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
                .SingleInstance()
                .AsSelf()
                ;

            builder.RegisterType<SessionManager>()
                .AttributedPropertiesAutowired()
                .SingleInstance()
                .As<ISessionManager>()
                ;

            builder.RegisterSource<SessionServiceRegistrationSource>();
        }
    }
}
