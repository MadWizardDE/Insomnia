using Autofac;
using Autofac.Core;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Network;
using MadWizard.Insomnia.NetworkSession;
using MadWizard.Insomnia.NetworkSession.Manager;
using MadWizard.Insomnia.Power;
using MadWizard.Insomnia.Power.Manager;
using MadWizard.Insomnia.Processes;
using MadWizard.Insomnia.Processes.Manager;
using MadWizard.Insomnia.Session;
using MadWizard.Insomnia.Session.Manager;
using NLog;

namespace MadWizard.Insomnia
{
    public class CoreModule(InsomniaConfig config) : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ActionManager>()
                .AsImplementedInterfaces()
                .SingleInstance()
                .AsSelf();

            if (config.NetworkMonitor.Count > 0)
            {
                //builder.RegisterType<NetworkSniffer>()
                //    .AsImplementedInterfaces()
                //    .InstancePerDependency()
                //    .AsSelf();

                foreach (var monitorConfig in config.NetworkMonitor)
                {
                    builder.RegisterType<NetworkSniffer>()
                        .WithParameter(new TypedParameter(typeof(INetworkInterfaceConfig), monitorConfig))
                        .AsImplementedInterfaces()
                        .SingleInstance()
                        .AsSelf();

                    builder.RegisterType<NetworkMonitor>()
                        .WithParameter(new TypedParameter(typeof(NetworkMonitorConfig), monitorConfig))
                        .AsImplementedInterfaces()
                        .SingleInstance()
                        .AsSelf();

                    //builder.Register(ctx =>
                    //{
                    //    var sniffer = ctx.Resolve<NetworkSniffer>(
                    //        new TypedParameter(typeof(INetworkSnifferConfig), monitorConfig)
                    //    );

                    //    var monitor = new NetworkMonitor(monitorConfig, sniffer);

                    //    ctx.InjectProperties(monitor);

                    //    return monitor;
                    //}).Named<NetworkMonitor>(monitorConfig.Name).AsImplementedInterfaces().AsSelf();

                    break; // FIXME add Support for more than one NetworkMonitor
                }
            }

            if (config.ProcessMonitor != null)
                builder.RegisterType<ProcessMonitor>()
                    .OnlyIf(reg => reg.IsRegistered(new TypedService(typeof(IProcessManager))))
                    .WithParameter(new TypedParameter(typeof(ProcessMonitorConfig), config.ProcessMonitor))
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf();

            if (config.PowerRequestMonitor != null)
                builder.RegisterType<PowerRequestMonitor>()
                    .OnlyIf(reg => reg.IsRegistered(new TypedService(typeof(IPowerManager))))
                    .WithParameter(new TypedParameter(typeof(PowerRequestMonitorConfig), config.PowerRequestMonitor))
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf();

            if (config.SessionMonitor != null)
                builder.RegisterType<SessionMonitor>()
                    .OnlyIf(reg => reg.IsRegistered(new TypedService(typeof(ISessionManager))))
                    .WithParameter(new TypedParameter(typeof(SessionMonitorConfig), config.SessionMonitor))
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf();

            if (config.NetworkSessionMonitor != null)
                builder.RegisterType<NetworkSessionMonitor>()
                    .OnlyIf(reg => reg.IsRegistered(new TypedService(typeof(INetworkSessionManager))))
                    .WithParameter(new TypedParameter(typeof(NetworkSessionMonitorConfig), config.NetworkSessionMonitor))
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf();

            builder.RegisterType<SystemMonitor>()
                .As<IStartable>()
                .SingleInstance()
                .AsSelf();

            builder.RegisterType<SleepWatch>()
                .AsImplementedInterfaces()
                .SingleInstance();

            builder.RegisterType<SystemUsageInspector>()
                .AsImplementedInterfaces()
                .SingleInstance()
                .AsSelf();
        }
    }
}
