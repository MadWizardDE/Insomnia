using Autofac;
using MadWizard.Insomnia.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.RemoteControl
{
    class RemoteControlModule : Module
    {
        RemoteControlConfig _config;

        public RemoteControlModule(RemoteControlConfig config)
        {
            _config = config;
        }

        protected override void Load(ContainerBuilder builder)
        {
            //if (_config.TrayMenu != null)
            //{
            //    builder.RegisterType<NotificationAreaController>()
            //        .AttributedPropertiesAutowired()
            //        .AsImplementedInterfaces()
            //        .SingleInstance()
            //        ;
            //}

            //if (_config.WindowCleaner != null)
            //{
            //    builder.RegisterType<WindowController>()
            //        .AttributedPropertiesAutowired()
            //        .AsImplementedInterfaces()
            //        .SingleInstance()
            //        ;
            //}
        }

    }
}