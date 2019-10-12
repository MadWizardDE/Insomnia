using Autofac;
using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.Sessions
{
    class SessionModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<SessionBridge>()
                .AttributedPropertiesAutowired()
                .AsImplementedInterfaces()
                .SingleInstance()
                .AsSelf()
                ;

            builder.RegisterType<SessionManager>()
                .AttributedPropertiesAutowired()
                .AsImplementedInterfaces()
                .As<ISessionManager>()
                .SingleInstance()
                ;

            builder.RegisterSource<SessionServiceRegistrationSource>();
        }
    }
}
