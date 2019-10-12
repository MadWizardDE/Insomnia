using Castle.DynamicProxy;
using NamedPipeWrapper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Timers;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;
using System.Linq;

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

        internal int ServiceCount => _services.Count;

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

        internal Task Terminate(double? timeout = null)
        {
            if (Process.HasExited)
                throw new InvalidOperationException("Process has exited.");

            var taskSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (timeout != null)
            {
                var timer = new Timer(timeout.Value);
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

            Terminated += (s, e) => taskSource.SetResult(true);

            _pipe.PushMessage(new TerminateMessage());

            return taskSource.Task;
        }

        private class ServiceProxy : IInterceptor
        {
            internal const int INVOCATION_TIMEOUT = 10000;

            SessionMinion __minion;

            Type _serviceType;

            Timer _timeoutTimer;

            IDictionary<long, ServiceInvocation> _invocationMap;

            long _nextMsgID = 1;

            internal ServiceProxy(SessionMinion minion, Type serviceType)
            {
                __minion = minion;

                _serviceType = serviceType;

                _timeoutTimer = new Timer();
                _timeoutTimer.Interval = 100;
                _timeoutTimer.AutoReset = true;
                _timeoutTimer.Elapsed += TimeoutTimer_Elapsed;

                _invocationMap = new ConcurrentDictionary<long, ServiceInvocation>();
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
                    ContinueWithResult(resultMessage);
                }
                else
                    throw new ArgumentException($"Unkown MessageType: {_invocationMap.GetType().Name}");
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
                return InterceptAsync(id).Result;
            }
            Task<object> InterceptAsync(long id)
            {
                _timeoutTimer.Enabled = true;

                var taskSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                _invocationMap.Add(id, new ServiceInvocation(id, taskSource, TimeSpan.FromMilliseconds(SERVICE_TIMEOUT)));
                return taskSource.Task;
            }

            internal void ContinueWithResult(ServiceInvocationResultMessage message)
            {
                if (!_invocationMap.TryGetValue(message.Id, out var invocation))
                    throw new ArgumentException($"Unknown Message {message.Id} ");

                try
                {
                    if (message.ExceptionValue != null)
                        invocation.TaskSource.SetException(message.ExceptionValue);
                    invocation.TaskSource.SetResult(message.ReturnValue);
                }
                finally
                {
                    _invocationMap.Remove(message.Id);
                }
            }
            internal void ContinueWithTimeout(long id)
            {
                if (!_invocationMap.TryGetValue(id, out var invocation))
                    throw new ArgumentException($"Unknown Message {id} ");

                try
                {
                    invocation.TaskSource.SetException(new TimeoutException("Service-Invocation timed out"));
                }
                finally
                {
                    _invocationMap.Remove(id);
                }
            }

            private void TimeoutTimer_Elapsed(object sender, ElapsedEventArgs e)
            {
                foreach (ServiceInvocation invocation in _invocationMap.Values.ToList())
                {
                    if (invocation.IsTimeout)
                        ContinueWithTimeout(invocation.Id);
                }

                if (_invocationMap.Count == 0)
                    _timeoutTimer.Stop();
            }

            class ServiceInvocation
            {
                internal ServiceInvocation(long id, TaskCompletionSource<object> source, TimeSpan? timeout = null)
                {
                    Id = id;

                    TaskSource = source;

                    InvocationTime = DateTime.Now;
                    MaxInvocationDuration = timeout;
                }

                internal long Id { get; private set; }

                internal TaskCompletionSource<object> TaskSource { get; set; }

                internal DateTime InvocationTime { get; }
                internal TimeSpan? MaxInvocationDuration { get; }
                internal bool IsTimeout
                {
                    get => MaxInvocationDuration.HasValue ? InvocationTime + MaxInvocationDuration > DateTime.Now : false;
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