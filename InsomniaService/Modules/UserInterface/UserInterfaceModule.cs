using Autofac;
using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;

namespace MadWizard.Insomnia.Service.UI
{
    class UserInterfaceModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<NotificationAreaController>()
                .AttributedPropertiesAutowired()
                .AsImplementedInterfaces()
                .SingleInstance()
                ;

            builder.RegisterType<WindowController>()
                .AttributedPropertiesAutowired()
                .AsImplementedInterfaces()
                .SingleInstance()
                ;
        }


    }
}