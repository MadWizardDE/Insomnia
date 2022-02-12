using Autofac;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.SleepWatch.Detector;
using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.SleepWatch
{
    class SleepWatchModule : Module
    {
        SleepWatchConfig _config;

        public SleepWatchModule(SleepWatchConfig config)
        {
            _config = config;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<SleepMonitor>()
                .AttributedPropertiesAutowired()
                .AsImplementedInterfaces()
                .SingleInstance()
                .AsSelf()
                ;

            if (_config.ActivityDetector != null)
            {
                builder.RegisterType<ActivityDetector>()
                    .AttributedPropertiesAutowired()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf()
                    ;

                #region Detectors
                if (_config.ActivityDetector.PingHost != null)
                    builder.RegisterType<PingHostDetector>()
                        .AttributedPropertiesAutowired()
                        .AsImplementedInterfaces()
                        .SingleInstance()
                        ;
                if (_config.ActivityDetector.PowerRequests != null)
                    builder.RegisterType<PowerRequestDetector>()
                    .AttributedPropertiesAutowired()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf()
                    ;
                if (_config.ActivityDetector.ProcessActivity != null)
                    builder.RegisterType<ProcessActivity>()
                    .AttributedPropertiesAutowired()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf()
                    ;
                if (_config.ActivityDetector.RemoteDesktopConnection != null)
                    builder.RegisterType<RemoteDesktopDetector>()
                    .AttributedPropertiesAutowired()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    ;
                if (_config.ActivityDetector.UserIdle != null)
                    builder.RegisterType<UserIdleDetector>()
                    .AttributedPropertiesAutowired()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    ;
                if (_config.ActivityDetector.WakeOnLAN != null)
                    builder.RegisterType<WakeOnLANAnalyzer>()
                    .AttributedPropertiesAutowired()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    ;
                if (_config.ActivityDetector.ManualOverride != null)
                    builder.RegisterType<ManualOverrideSwitch>()
                    .AttributedPropertiesAutowired()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf()
                    ;
                #endregion
                #region Examiners
                builder.RegisterType<ActivityDetector.SleepInhibitor>()
                    .AttributedPropertiesAutowired()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf()
                    ;

                if (_config.ActivityDetector.IdleMax.HasValue)
                    builder.RegisterType<ActivityDetector.SleepEnforcer>()
                        .WithParameter(new TypedParameter(typeof(int), _config.ActivityDetector.IdleMax))
                        .AttributedPropertiesAutowired()
                        .AsImplementedInterfaces()
                        .SingleInstance()
                        .AsSelf()
                        ;
                #endregion
            }

            if (_config.AntiSleepWalk != null)
                builder.RegisterType<AntiSleepWalk>()
                    .AttributedPropertiesAutowired()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    ;

            if (_config.AntiGhost != null)
                builder.RegisterType<AntiGhost>()
                    .AttributedPropertiesAutowired()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    ;

            if (_config.Log)
                builder.RegisterType<SleepLogWriter>()
                    .AttributedPropertiesAutowired()
                    .SingleInstance()
                    .AsImplementedInterfaces()
                    .AsSelf()
                    ;

            #region NetworkCommander
            if (_config.NetworkCommander != null)
            {
                builder.RegisterType<NetworkCommander>()
                    .AttributedPropertiesAutowired()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf()
                    ;

                builder.RegisterType<NetworkCommander.WakeModeNone>()
                    .As<NetworkCommander.IWakeMode>()
                    .AttributedPropertiesAutowired()
                    .SingleInstance();
                builder.RegisterType<NetworkCommander.WakeModeWOL>()
                    .As<NetworkCommander.IWakeMode>()
                    .AttributedPropertiesAutowired()
                    .SingleInstance();
            }
            #endregion

        }
    }
}
