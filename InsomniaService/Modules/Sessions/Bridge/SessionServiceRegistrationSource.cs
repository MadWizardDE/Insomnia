using Autofac;
using Autofac.Builder;
using Autofac.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MadWizard.Insomnia.Service.Sessions
{
    class SessionServiceRegistrationSource : IRegistrationSource
    {
        bool IRegistrationSource.IsAdapterForIndividualComponents => throw new NotImplementedException();

        IEnumerable<IComponentRegistration> IRegistrationSource.RegistrationsFor(
            Autofac.Core.Service service,
            Func<Autofac.Core.Service, IEnumerable<IComponentRegistration>> registrationAccessor)
        {
            if (!(service is IServiceWithType swt) || !swt.ServiceType.IsGenericType || swt.ServiceType.GetGenericTypeDefinition() != typeof(ISessionService<>))
                yield break;

            var valueType = swt.ServiceType.GetGenericArguments()[0];

            yield return (IComponentRegistration)CreateLazyRegistrationMethod.MakeGenericMethod(valueType).Invoke(null, new object[] { service });
        }

        static readonly MethodInfo CreateLazyRegistrationMethod =
            typeof(SessionServiceRegistrationSource)
                .GetMethod(nameof(CreateSessionServiceRegistration),
                    BindingFlags.Static | BindingFlags.NonPublic);

        static IComponentRegistration CreateSessionServiceRegistration<T>(Autofac.Core.Service providedService) where T : class
        {
            var rb = RegistrationBuilder.ForDelegate(
                (c, p) =>
                {
                    var bridge = c.Resolve<SessionBridge>();

                    return bridge.AcquireSessionServiceReference<ISessionService<T>>();
                })
                .As(providedService);

            return rb.CreateRegistration();
        }
    }
}