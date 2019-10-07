using Castle.DynamicProxy;
using NamedPipeWrapper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service.Sessions
{
    class SessionMinion
    {
        internal const int SERVICE_TIMEOUT = 1000;

        static readonly ProxyGenerator Generator = new ProxyGenerator();

        SessionMinionConfig _config;

        NamedPipeConnection<Message, Message> _pipe;

        Dictionary<Type, ServiceProxy> _services;

        bool _forcedKill = false;

        internal SessionMinion(ISession session, Process process, NamedPipeConnection<Message, Message> pipe, SessionMinionConfig config)
        {
            if (session == null || process == null || pipe == null)
                throw new ArgumentException();

            _services = new Dictionary<Type, ServiceProxy>();

            Session = session;
            Process = process;
            Process.EnableRaisingEvents = true;
            Process.Exited += Process_Exited;

            _pipe = pipe;
            _pipe.ReceiveMessage += Pipe_ReceiveMessage;
            _pipe.Disconnected += Pipe_Disconnected;

            _pipe.PushMessage(new StartupMessage(Config = config));
        }

        internal SessionMinionConfig Config { get; private set; }

        internal ISession Session { get; private set; }
        internal Process Process { get; private set; }

        internal int PID => Process.Id;
        internal int SID => Process.SessionId;

        internal event EventHandler<MessageEventArgs> MessageArrived;
        internal event EventHandler<TerminationEventArgs> Terminated;

        #region Pipe & Process
        internal bool Matches(NamedPipeConnection<Message, Message> pipe) => _pipe == pipe;

        void Pipe_ReceiveMessage(NamedPipeConnection<Message, Message> connection, Message message)
        {
            if (message is ServiceMessage svcMessage)
            {
                var proxy = _services[svcMessage.ServiceType];

                proxy.HandleMessage(svcMessage);
            }
            else if (message is UserMessage customMessage)
            {
                MessageArrived?.Invoke(this, new MessageEventArgs(customMessage));
            }
            else
                throw new ArgumentException($"MessageType[{typeof(Message).Name}] unknown");
        }
        void Pipe_Disconnected(NamedPipeConnection<Message, Message> connection)
        {
            Thread.Sleep(500);

            if (!Process.HasExited)
            {
                _forcedKill = true;

                Process.Kill();
            }
        }
        void Process_Exited(object sender, EventArgs e)
        {
            Terminated?.Invoke(this, new TerminationEventArgs(_forcedKill));
        }
        #endregion

        #region Service-Control
        internal async Task<T> StartService<T>() where T : class
        {
            if (_services.ContainsKey(typeof(T)))
                throw new InvalidOperationException("Service already started.");

            var proxy = new ServiceProxy(this, typeof(T));

            _services.Add(typeof(T), proxy);

            _pipe.PushMessage(new ServiceControlMessage(typeof(T), ServiceState.STARTED));

            try
            {
                int time = 0;
                while (proxy.State != ServiceState.STARTED)
                {
                    if ((time += 10) > SERVICE_TIMEOUT)
                        throw new TimeoutException();

                    await Task.Delay(10);
                }
            }
            catch (TimeoutException)
            {
                _services.Remove(typeof(T));

                throw;
            }

            return Generator.CreateInterfaceProxyWithoutTarget<T>(proxy);
        }

        internal async Task StopService<T>() where T : class
        {
            if (!_services.ContainsKey(typeof(T)))
                throw new InvalidOperationException("Service not started.");

            var proxy = _services[typeof(T)];

            _pipe.PushMessage(new ServiceControlMessage(typeof(T), ServiceState.STOPPED));

            int time = 0;
            while (proxy.State != ServiceState.STOPPED)
            {
                if ((time += 10) > SERVICE_TIMEOUT)
                    throw new TimeoutException();

                await Task.Delay(10);
            }

            _services.Remove(typeof(T));
        }
        #endregion

        internal void SendMessage(UserMessage message)
        {
            _pipe.PushMessage(message);
        }

        internal void Terminate(double? timeout = null, bool wait = false)
        {
            using (var waiter = new ManualResetEvent(false))
            {
                if (wait)
                {
                    Terminated += (s, e) => waiter.Set();
                }

                _pipe.PushMessage(new TerminateMessage());

                if (timeout != null)
                {
                    var timer = new System.Timers.Timer(timeout.Value);
                    timer.AutoReset = false;
                    timer.Elapsed += (t, e) =>
                    {
                        if (!Process.HasExited)
                        {
                            _forcedKill = true;

                            Process.Kill();
                        }
                    };

                    timer.Start();
                }

                if (wait)
                {
                    waiter.WaitOne();
                }
            }
        }

        private class ServiceProxy : IInterceptor
        {
            internal const int INVOCATION_TIMEOUT = 10000;

            SessionMinion __minion;

            Type _serviceType;

            IDictionary<long, ServiceInvocationResultMessage> _resultMap;

            long _nextMsgID = 1;

            internal ServiceProxy(SessionMinion minion, Type serviceType)
            {
                __minion = minion;

                _serviceType = serviceType;

                _resultMap = new ConcurrentDictionary<long, ServiceInvocationResultMessage>();
            }

            internal ServiceState State { get; private set; }

            internal void HandleMessage(ServiceMessage svcMessage)
            {
                if (svcMessage is ServiceStateMessage statusMessage)
                {
                    State = statusMessage.State;
                }
                else if (svcMessage is ServiceInvocationResultMessage resultMessage)
                {
                    _resultMap.Add(resultMessage.Id, resultMessage);
                }
                else
                    throw new ArgumentException($"Unkown MessageType: {_resultMap.GetType().Name}");
            }

            void IInterceptor.Intercept(IInvocation invocation)
            {
                ServiceInvocationMessage message =
                    new ServiceInvocationMessage(_serviceType, _nextMsgID++,
                        invocation.Method, invocation.Arguments);

                __minion._pipe.PushMessage(message);

                if (invocation.Method.ReturnType == typeof(Task)
                    || invocation.Method.ReturnType.BaseType == typeof(Task))
                    invocation.ReturnValue = InterceptAsync(message.Id);
                else
                    invocation.ReturnValue = InterceptSync(message.Id);
            }

            object InterceptSync(long id)
            {
                try
                {
                    int time = 0;
                    while (true)
                    {
                        if (!_resultMap.TryGetValue(id, out var resultMessage))
                        {
                            if (time > INVOCATION_TIMEOUT)
                                throw new TimeoutException("Service-Invocation timed out");

                            Thread.Sleep(10); // sleepwait
                            time += 10;
                        }
                        else
                        {
                            if (resultMessage.ExceptionValue != null)
                                throw resultMessage.ExceptionValue;
                            return resultMessage.ReturnValue;
                        }
                    }
                }
                finally
                {
                    _resultMap.Remove(id);
                }
            }
            async Task<object> InterceptAsync(long id)
            {
                try
                {
                    int time = 0;
                    while (true)
                    {
                        if (!_resultMap.TryGetValue(id, out var resultMessage))
                        {
                            if (time > INVOCATION_TIMEOUT)
                                throw new TimeoutException("Service-Invocation timed out");

                            await Task.Delay(10); // delaywait
                            time += 10;
                        }
                        else
                        {
                            if (resultMessage.ExceptionValue != null)
                                throw resultMessage.ExceptionValue;
                            return resultMessage.ReturnValue;
                        }
                    }
                }
                finally
                {
                    _resultMap.Remove(id);
                }
            }
        }

        #region Events
        internal class MessageEventArgs : EventArgs
        {
            internal MessageEventArgs(UserMessage message)
            {
                Message = message;
            }

            public UserMessage Message { get; private set; }
        }
        internal class TerminationEventArgs : EventArgs
        {
            internal TerminationEventArgs(bool forcedKill)
            {
                ForcedKill = forcedKill;
            }

            public bool ForcedKill { get; private set; }
        }
        #endregion

        #region Exceptions
        internal class ServiceErrorException : Exception
        {

        }
        #endregion
    }
}