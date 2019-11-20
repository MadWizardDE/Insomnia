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
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static MadWizard.Insomnia.Service.Sessions.SessionMinion;

namespace MadWizard.Insomnia.Service.Sessions
{
    class SessionBridge : IDisposable
    {
        internal const int MINION_TIMEOUT = 5000;

        IComponentContext _compContext;

        InsomniaConfig _config;

        ISessionManager _sessionManager;

        NamedPipeServer<Message> _pipeServer;

        IDictionary<Type, SessionService> _services;
        IDictionary<ISession, SessionMinion> _minions;

        public SessionBridge(IComponentContext compContext, InsomniaConfig config, ISessionManager sessionManager, ILogger<SessionBridge> logger)
        {
            _compContext = compContext;

            _config = config;

            _sessionManager = sessionManager;

            _sessionManager.UserLogin += SessionManager_UserLogin;

            _services = new ConcurrentDictionary<Type, SessionService>();
            _minions = new ConcurrentDictionary<ISession, SessionMinion>();

            _pipeServer = new NamedPipeServer<Message>(Message.PIPE_NAME, new PipeSecurity());
            _pipeServer.ClientConnected += PipeServer_ClientConnected;
            _pipeServer.Error += PipeServer_Error;
            _pipeServer.Start();

            logger.LogDebug("SessionBridge started");
        }

        [Autowired]
        ILogger<SessionBridge> Logger { get; set; }

        #region SessionManager-Callbacks
        private void SessionManager_UserLogin(object sender, SessionLoginEventArgs args)
        {
            if (args.IsSessionCreated)
                foreach (SessionService service in _services.Values)
                    service.AddSession(args.Session);
        }
        #endregion

        #region SessionService(-References)
        internal SessionServiceReference<T> AcquireSessionServiceReference<T>() where T : class
        {
            if (!_services.TryGetValue(typeof(T), out SessionService service))
                service = CreateSessionService<T>();

            Logger.LogDebug($"SessionService<{service.ServiceType.Name}>-Reference aquired");

            return new SessionServiceReference<T>((SessionService<T>)service);
        }

        private void SessionService_ReferencesChanged(object sender, EventArgs args)
        {
            if (sender is SessionService service)
            {
                Logger.LogDebug($"SessionService<{service.ServiceType.Name}>-#Reference = {service.ReferenceCount}");

                if (service.ReferenceCount == 0)
                {
                    DestroySessionService(service);
                }
            }
        }

        private SessionService<T> CreateSessionService<T>() where T : class
        {
            SessionService<T> service = new SessionService<T>(this);
            service.ReferencesChanged += SessionService_ReferencesChanged;

            foreach (ISession session in _sessionManager.Sessions)
                service.AddSession(session);

            _services.Add(typeof(T), service);

            Logger.LogDebug($"SessionService<{service.ServiceType.Name}> created");

            return service;
        }
        private void DestroySessionService(SessionService service)
        {
            service.ReferencesChanged -= SessionService_ReferencesChanged;
            service.Dispose();

            _services.Remove(service.ServiceType);

            Logger.LogDebug($"SessionService<{service.ServiceType.Name}> destroyed");
        }
        #endregion

        #region Service-References
        internal async Task<IServiceReference<T>> AcquireServiceReference<T>(ISession session) where T : class
        {
            if (!_minions.TryGetValue(session, out SessionMinion minion))
            {
                Logger.LogDebug($"Launching SessionMinion[{session.Id}]...");

                minion = await LaunchMinion(session.Id, MINION_TIMEOUT);
            }

            if (!minion.IsConnected)
                throw new InvalidOperationException($"Minion[{session.Id}] not connected");

            var service = new ServiceReference<T>(session, await minion.StartService<T>());

            Logger.LogDebug($"Minion[{minion.Session.Id}] started Service<{typeof(T).Name}>");

            return service;
        }
        internal async Task ReleaseServiceReference<T>(IServiceReference<T> serviceRef) where T : class
        {
            if (!_minions.TryGetValue(serviceRef.Session, out SessionMinion minion))
                throw new InvalidOperationException($"Minion[{serviceRef.Session.Id}] not started");

            if (minion.IsConnected)
            {
                await minion.StopService<T>();

                Logger.LogDebug($"Minion[{minion.Session.Id}] stopped Service<{typeof(T).Name}>");

                if (minion.ServiceCount == 0)
                {
                    Logger.LogDebug($"Terminating SessionMinion<{serviceRef.Session.Id}>...");

                    await minion.Terminate(MINION_TIMEOUT);
                }
            }
        }
        #endregion

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
                                Interval = _config.Interval / 10,
                            };

                            SessionMinion minion = new SessionMinion(session, process, pipe, config);
                            AddMinion(minion);

                            Logger.LogDebug(InsomniaEventId.SESSION_MINION_STARTED, $"SessionMinion connected (SID={minion.SID}, PID={minion.PID})");
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

            if (_config.Logging.FileSystemLog != null && _config.Logging.LogLevelMinion != LogLevel.None)
                args.Append($" -LogLevel={_config.Logging.LogLevelMinion}");

            var cd = Directory.GetCurrentDirectory();
            var cmd = $"{Path.Combine(cd, "bin", "InsomniaSessionMinion.exe")} {args}";

            // CREATE PROCESS
            int pid = Win32API.CreateProcessInSession(cmd, cd, (uint)sid);
            // CREATE PROCESS

            if (Logger.IsEnabled(LogLevel.Debug))
                Logger.LogDebug($"SessionMinion started (PID={pid})");

            int time = 0;
            while (timeout == null || (time += 100) < timeout)
            {
                if (MinionByPID(pid) != null)
                    return MinionByPID(pid);

                await Task.Delay(100);
            }

            throw new TimeoutException();
        }

        private void AddMinion(SessionMinion minion)
        {
            minion.MessageArrived += Minion_MessageArrived;
            minion.Terminated += Minion_Terminated;

            _minions.Add(minion.Session, minion);
        }
        private void RemoveMinion(SessionMinion minion)
        {
            minion.Terminated -= Minion_Terminated;
            minion.MessageArrived -= Minion_MessageArrived;

            foreach (SessionService service in _services.Values)
                service.RemoveSession(minion.Session);
            _minions.Remove(minion.Session);
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

        private void Minion_HandleMessage<T>(ISession session, T message) where T : UserMessage
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

            var method = typeof(SessionBridge).GetMethod(nameof(Minion_HandleMessage), BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(args.Message.GetType());

            method.Invoke(this, new object[] { session, args.Message });
        }
        private void Minion_Terminated(object sender, SessionMinion.TerminationEventArgs args)
        {
            var minion = sender as SessionMinion;

            if (args.ForcedKill) Logger.LogError(InsomniaEventId.SESSION_MINION_STOPPED, $"SessionMinion terminated (SID={minion.SID}, PID={minion.PID}) -> hung");
            else Logger.LogDebug(InsomniaEventId.SESSION_MINION_STOPPED, $"SessionMinion disconnected (SID={minion.SID}, PID={minion.PID})");

            RemoveMinion(minion);
        }
        #endregion

        void IDisposable.Dispose()
        {
            Logger.LogDebug("SessionBridge shutting down...");

            foreach (SessionService srv in _services.Values.ToArray())
                DestroySessionService(srv);

            _pipeServer.Stop();

            Logger.LogDebug("SessionBridge stopped");
        }
    }
}