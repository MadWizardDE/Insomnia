using Autofac;
using Autofac.Features.OwnedInstances;
using MadWizard.Insomnia.Service;
using MadWizard.Insomnia.Service.Sessions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Minion
{
    class ServiceManager : IStartable, IServiceMessageHandler, IDisposable
    {
        ILifetimeScope _lifetimeScope;

        ISystemMessenger _messenger;

        IDictionary<Type, Service> _services;

        public ServiceManager(ILifetimeScope scope, ISystemMessenger messenger)
        {
            _lifetimeScope = scope;

            _messenger = messenger;

            _services = new Dictionary<Type, Service>();
        }

        [Autowired]
        ILogger<ServiceManager> Logger { get; set; }

        void IServiceMessageHandler.HandleMessage(ServiceMessage message)
        {
            try
            {
                switch (message)
                {
                    case ServiceControlMessage control:
                        switch (control.State)
                        {
                            case ServiceState.STARTED:
                                StartService(message.ServiceType);
                                break;

                            case ServiceState.STOPPED:
                                StopService(message.ServiceType);
                                break;

                            default:
                                throw new ArgumentException($"Invalid ServiceState[{message.ServiceType.Name}]");
                        }
                        break;

                    case ServiceInvocationMessage invocation:
                        if (!_services.TryGetValue(message.ServiceType, out Service service))
                            throw new InvalidOperationException("Service is not started");

                        try
                        {
                            object result = service.HandleInvocation(invocation.Method, invocation.Arguments);

                            _messenger.SendMessage(new ServiceInvocationResultMessage(message.ServiceType, invocation.Id, returnValue: result));
                        }
                        catch (Exception exception)
                        {
                            _messenger.SendMessage(new ServiceInvocationResultMessage(message.ServiceType, invocation.Id, exceptionValue: exception));
                        }

                        break;
                }
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogWarning(ex, $"{message.GetType().Name} failed");
            }
        }

        void IStartable.Start()
        {
            Logger.LogDebug($"{nameof(ServiceManager)} started");
        }

        #region Service-Control
        private void StartService(Type serviceType)
        {
            if (_services.ContainsKey(serviceType))
                throw new InvalidOperationException("Service already started");

            ILifetimeScope serviceScope = _lifetimeScope.BeginLifetimeScope(serviceType);

            _services.Add(serviceType, new Service(serviceType, serviceScope));

            Logger.LogDebug($"{serviceType.Name} started");

            _messenger.SendMessage(new ServiceStateMessage(serviceType, ServiceState.STARTED));
        }
        private void StopService(Type serviceType)
        {
            if (!_services.TryGetValue(serviceType, out Service service))
                throw new InvalidOperationException("Service is not started");

            service.Stop();

            _services.Remove(serviceType);

            Logger.LogDebug($"{serviceType.Name} stopped");

            _messenger.SendMessage(new ServiceStateMessage(serviceType, ServiceState.STOPPED));
        }
        #endregion

        void IDisposable.Dispose()
        {
            foreach (var serviceType in _services.Keys.ToArray())
                StopService(serviceType);
        }

        private class Service
        {
            const int SERVICE_TIMEOUT = 5000;

            Type _serviceType;

            ILifetimeScope _serviceScope;

            public Service(Type type, ILifetimeScope scope)
            {
                _serviceType = type;
                _serviceScope = scope;

                object obj = _serviceScope.Resolve(_serviceType);

                if (obj is IHostedService hostedService)
                {
                    using CancellationTokenSource cancelSource = new CancellationTokenSource(SERVICE_TIMEOUT);

                    hostedService.StartAsync(cancelSource.Token).Wait();
                }
            }

            public object HandleInvocation(MethodInfo method, object[] parameters)
            {
                object obj = _serviceScope.Resolve(_serviceType);

                object result = method.Invoke(obj, parameters);

                if (result is Task task)
                {
                    if (method.ReturnType.IsGenericType)
                    {
                        var property = task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);

                        result = property.GetValue(task);
                    }
                    else
                        result = null;
                }

                return result;
            }

            public void Stop()
            {
                object obj = _serviceScope.Resolve(_serviceType);

                if (obj is IHostedService hostedService)
                {
                    using CancellationTokenSource cancelSource = new CancellationTokenSource(SERVICE_TIMEOUT);

                    hostedService.StopAsync(cancelSource.Token).Wait();
                }

                _serviceScope.Dispose();
            }
        }
    }
}