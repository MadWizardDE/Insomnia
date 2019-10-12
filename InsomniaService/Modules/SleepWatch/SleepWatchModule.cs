using Autofac;
using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.SleepWatch
{
    class SleepWatchModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ActivityDetector>()
                .AttributedPropertiesAutowired()
                .AsImplementedInterfaces()
                .SingleInstance()
                .AsSelf()
                ;

            builder.RegisterType<AntiSleepWalk>()
                .AttributedPropertiesAutowired()
                .AsImplementedInterfaces()
                .SingleInstance()
                ;

            builder.RegisterType<SleepMonitor>()
                .AttributedPropertiesAutowired()
                .AsImplementedInterfaces()
                .SingleInstance()
                .AsSelf()
                ;

            builder.RegisterType<SleepLogWriter>()
                .AttributedPropertiesAutowired()
                .SingleInstance()
                .AsImplementedInterfaces()
                .AsSelf()
                ;

            #region NetworkCommander
            builder.RegisterType<NetworkCommander>()
                .AttributedPropertiesAutowired()
                .SingleInstance()
                .AsImplementedInterfaces()
                //.AsSelf()
                ;

            builder.RegisterType<NetworkCommander.WakeModeNone>()
                .As<NetworkCommander.IWakeMode>()
                .AttributedPropertiesAutowired()
                .SingleInstance();
            builder.RegisterType<NetworkCommander.WakeModeWOL>()
                .As<NetworkCommander.IWakeMode>()
                .AttributedPropertiesAutowired()
                .SingleInstance();
            #endregion

        }
    }
}
