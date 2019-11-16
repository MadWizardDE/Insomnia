using Autofac;
using MadWizard.Insomnia.Configuration;
using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;

namespace MadWizard.Insomnia.Service.UI
{
    class UserInterfaceModule : Module
    {
        UserInterfaceConfig _config;

        public UserInterfaceModule(UserInterfaceConfig config)
        {
            _config = config;
        }

        protected override void Load(ContainerBuilder builder)
        {
            if (_config.TrayMenu != null)
            {
                builder.RegisterType<NotificationAreaController>()
                    .AttributedPropertiesAutowired()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    ;
            }

            if (_config.WindowCleaner != null)
            {
                builder.RegisterType<WindowController>()
                    .AttributedPropertiesAutowired()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    ;
            }
        }


    }
}