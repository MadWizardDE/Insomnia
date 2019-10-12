using Autofac;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Tools;
using Microsoft.Extensions.Logging;
using NamedPipeWrapper;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static MadWizard.Insomnia.Service.Sessions.SessionMinion;

namespace MadWizard.Insomnia.Service.Sessions
{
    class SessionBridge : IDisposable
    {
        internal const int MINION_TIMEOUT = 50000;

        IComponentContext _compContext;

        InsomniaConfig _config;

        ISessionManager _sessionManager;

        NamedPipeServer<Message> _pipeServer;

        IDictionary<Type, SessionService> _services;
        IDictionary<ISession, SessionMinion> _minions;

        public SessionBridge(IComponentContext compContext, InsomniaConfig config, ISessionManager sessionManager)
        {
            _compContext = compContext;

            _config = config;

            _sessionManager = sessionManager;

            _sessionManager.UserLogin += SessionManager_UserLogin;
            _sessionManager.UserLogout += SessionManager_UserLogout;

            _services = new ConcurrentDictionary<Type, SessionService>();
            _minions = new ConcurrentDictionary<ISession, SessionMinion>();

            _pipeServer = new NamedPipeServer<Message>(Message.PIPE_NAME, new PipeSecurity());
            _pipeServer.ClientConnected += PipeServer_ClientConnected;
            _pipeServer.Error += PipeServer_Error;
            _pipeServer.Start();
        }

        [Autowired]
        ILogger<SessionBridge> Logger { get; set; }

        #region SessionManager-Callbacks
        private void SessionManager_UserLogin(object sender, UserEventArgs args)
        {
            foreach (SessionService service in _services.Values)
                service.AddSession(args.Session);
        }
        private void SessionManager_UserLogout(object sender, UserEventArgs args)
        {
            foreach (SessionService service in _services.Values)
                service.RemoveSession(args.Session);
            _minions.Remove(args.Session);
        }
        #endregion

        #region SessionService(-References)
        internal SessionServiceReference<T> AcquireSessionServiceReference<T>() where T : class
        {
            if (!_services.TryGetValue(typeof(T), out SessionService service))
                service = CreateSessionService<T>();

            var sessionServiceRef = new SessionServiceReference<T>((SessionService<T>)service);
            service.AddReference(sessionServiceRef);
            return sessionServiceRef;
        }

        private void SessionService_ReferencesChanged(object sender, EventArgs args)
        {
            if (sender is SessionService service && service.ReferenceCount == 0)
            {
                DestroySessionService(service);
            }
        }

        private SessionService<T> CreateSessionService<T>() where T : class
        {
            SessionService<T> service = new SessionService<T>(this);
            service.ReferencesChanged += SessionService_ReferencesChanged;

            foreach (ISession session in _sessionManager.Sessions)
                service.AddSession(session);

            _services.Add(typeof(T), service);

            return service;
        }
        private void DestroySessionService(SessionService service)
        {
            service.ReferencesChanged -= SessionService_ReferencesChanged;
            service.Dispose();

            _services.Remove(service.ServiceType);
        }
        #endregion

        #region Service-References
        internal async Task<IServiceReference<T>> AcquireServiceReference<T>(ISession session) where T : class
        {
            if (!_minions.TryGetValue(session, out SessionMinion minion))
            {
                Logger.LogDebug($"Launching SessionMinion<{session.Id}>...");

                minion = await LaunchMinion(session.Id, MINION_TIMEOUT);
            }

            return new ServiceReference<T>(session, await minion.StartService<T>());
        }
        internal async Task ReleaseServiceReference<T>(IServiceReference<T> serviceRef) where T : class
        {
            if (!_minions.TryGetValue(serviceRef.Session, out SessionMinion minion))
                throw new InvalidOperationException("Minion not started");

            await minion.StopService<T>();

            if (minion.ServiceCount == 0)
            {
                Logger.LogDebug($"Terminating SessionMinion<{serviceRef.Session.Id}>...");

                await minion.Terminate();
            }
        }
        #endregion

        List<NamedPipeConnection<Message, Message>> _conns = new List<NamedPipeConnection<Message, Message>>();

        #region PipeServer
        private void PipeServer_ClientConnected(NamedPipeConnection<Message, Message> pipe)
        {
            #region Transient-Connection Callbacks
            void PipeConnection_ReceiveMessage(NamedPipeConnection<Message, Message> pipe, Message message)
            {
                try
                {
                    if (message is IncarnationMessage)
                    {
                        pipe.ReceiveMessage -= PipeConnection_ReceiveMessage;
                        pipe.Disconnected -= PipeConnection_Disconnected;
                        pipe.Error -= PipeConnection_Error;

                        var process = Process.GetProcessById(((IncarnationMessage)message).PID);
                        var session = _sessionManager[process.SessionId];

                        if (_minions.ContainsKey(session))
                        {
                            Logger.LogWarning(InsomniaEventId.SESSION_MINION_ERROR, $"SessionMinion redundant (SID={session.Id}, PID={process.Id})");

                            pipe.PushMessage(new TerminateMessage());
                        }
                        else
                        {
                            var config = new SessionMinionConfig
                            {
                                Interval = _config.Interval / 10
                            };

                            SessionMinion minion = new SessionMinion(session, process, pipe, config);
                            minion.MessageArrived += Minion_MessageArrived;
                            minion.Terminated += Minion_Terminated;
                            _minions.Add(minion.Session, minion);

                            Logger.LogDebug(InsomniaEventId.SESSION_MINION_STARTED, $"SessionHelper connected (SID={minion.SID}, PID={minion.PID})");
                        }
                    }
                }
                catch (Exception exception)
                {
                    PipeConnection_Error(pipe, exception);
                }
            }
            void PipeConnection_Error(NamedPipeConnection<Message, Message> pipe, Exception exception)
            {
                Logger.LogError(InsomniaEventId.SESSION_MINION_ERROR, exception, $"SessionMinion connection failed");

                pipe.ReceiveMessage -= PipeConnection_ReceiveMessage;
                pipe.Disconnected -= PipeConnection_Disconnected;
                pipe.Error -= PipeConnection_Error;
            }
            void PipeConnection_Disconnected(NamedPipeConnection<Message, Message> pipe)
            {
                PipeConnection_Error(pipe, null);
            }
            #endregion

            _conns.Add(pipe);

            /*
             * We wait until the SessionMinion reports for duty.
             */
            pipe.ReceiveMessage += PipeConnection_ReceiveMessage;
            pipe.Disconnected += PipeConnection_Disconnected;
            pipe.Error += PipeConnection_Error;
        }
        private void PipeServer_Error(Exception exception)
        {
            Logger.LogError(exception, "PipeServer-Error");
        }
        #endregion

        #region Minions
        private async Task<SessionMinion> LaunchMinion(int sid, int? timeout = null)
        {
            var args = new StringBuilder();
            int? startupDelay = _config.DebugParameters?.StartupDelay;
            if ((startupDelay ?? 0) > 0)
            {
                args.Append($" -StartupDelay={startupDelay}");

                if (timeout != null)
                    timeout += startupDelay;
            }

            if (Logger.IsEnabled(LogLevel.Debug))
                args.Append($" -DebugLog");

            // CREATE PROCESS
            int pid = Win32API.CreateProcessInSession($"InsomniaSessionMinion.exe {args}", (uint)sid);
            // CREATE PROCESS

            if (Logger.IsEnabled(LogLevel.Debug))
                Logger.LogDebug($"SessionHelper started (PID={pid})");

            int time = 0;
            while (timeout == null || (time += 100) < timeout)
            {
                if (MinionByPID(pid) != null)
                    return MinionByPID(pid);

                await Task.Delay(100);
            }

            throw new TimeoutException();
        }

        private SessionMinion MinionByPID(int pid)
        {
            try
            {
                return _minions.Values.First(i => i.PID == pid);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
        private SessionMinion MinionBySID(int sid)
        {
            try
            {
                return _minions.Values.First(i => i.SID == sid);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
        private void HandleMessage<T>(ISession session, T message) where T : UserMessage
        {
            var handlers = _compContext.Resolve<IEnumerable<ISessionMessageHandler<T>>>();

            foreach (var handler in handlers)
                using (Logger.BeginScope("Invoking Handler: {HandlerType}", handler.GetType()))
                    try
                    {
                        handler.Handle(session, message);
                    }
                    catch (Exception exception)
                    {
                        Logger.LogError(exception, "SessionMessageHandler-Error");
                    }
        }
        private void Minion_MessageArrived(object sender, SessionMinion.MessageEventArgs args)
        {
            var session = (sender as SessionMinion).Session;

            var method = typeof(SessionBridge).GetMethod(nameof(HandleMessage)).MakeGenericMethod(args.Message.GetType());

            method.Invoke(this, new object[] { session, args.Message });
        }
        private void Minion_Terminated(object sender, SessionMinion.TerminationEventArgs args)
        {
            var minion = sender as SessionMinion;

            if (args.ForcedKill) Logger.LogError(InsomniaEventId.SESSION_MINION_STOPPED, $"SessionMinion terminated (SID={minion.SID}, PID={minion.PID}) -> hung");
            else Logger.LogDebug(InsomniaEventId.SESSION_MINION_STOPPED, $"SessionMinion disconnected (SID={minion.SID}, PID={minion.PID})");

            minion.Terminated -= Minion_Terminated;
            _minions.Remove(minion.Session);
        }
        #endregion

        void IDisposable.Dispose()
        {
            _pipeServer.Stop();
        }
    }
}