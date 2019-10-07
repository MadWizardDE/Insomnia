using Autofac;
using Autofac.Extensions.DependencyInjection;
using MadWizard.Insomnia.Minion.Services;
using MadWizard.Insomnia.Minion.Tools;
using MadWizard.Insomnia.Service;
using MadWizard.Insomnia.Service.Sessions;
using Microsoft.Extensions.Hosting;
using NamedPipeWrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using Message = MadWizard.Insomnia.Service.Sessions.Message;

namespace MadWizard.Insomnia.Minion
{
    static class Program
    {
        static void Main(string[] args)
        {
            if (Environment.OSVersion.Version.Major >= 6)
                Win32API.SetProcessDPIAware();

            IHost host;
            using (MinionBootstrap boot = new MinionBootstrap(args))
            {
                boot.WaitForStartup();

                host = CreateHostBuilder(args, boot).Build();
            }

            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args, MinionBootstrap boot) =>
            Host.CreateDefaultBuilder(args)
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureContainer<ContainerBuilder>((ctx, builder) =>
                {
                    builder.RegisterInstance(boot.Config);

                    #region System-Services
                    builder.RegisterType<PipeMessageBroker>()
                        .WithParameter(new TypedParameter(typeof(NamedPipeClient<Message>), boot.PipeClient))
                        .AttributedPropertiesAutowired()
                        .AsImplementedInterfaces()
                        .SingleInstance()
                        ;

                    builder.RegisterType<ServiceManager>()
                        .AttributedPropertiesAutowired()
                        .SingleInstance()
                        .AsSelf()
                        ;

                    builder.RegisterType<MinionApplicationContext>()
                        .AttributedPropertiesAutowired()
                        .AsImplementedInterfaces()
                        .SingleInstance()
                        ;
                    #endregion

                    #region Custom-Services
                    builder.RegisterType<UserIdleTimeService>()
                        .InstancePerMatchingLifetimeScope(typeof(IUserIdleTimeService))
                        .AttributedPropertiesAutowired()
                        .AsImplementedInterfaces()
                        ;
                    builder.RegisterType<WindowCleanerService>()
                        .InstancePerMatchingLifetimeScope(typeof(IWindowCleanerService))
                        .AttributedPropertiesAutowired()
                        .AsImplementedInterfaces()
                        ;
                    builder.RegisterType<TextMessageService>()
                        .InstancePerMatchingLifetimeScope(typeof(ITextMessageService))
                        .AttributedPropertiesAutowired()
                        .AsImplementedInterfaces()
                        ;
                    builder.RegisterType<NotificationAreaService>()
                        .InstancePerMatchingLifetimeScope(typeof(INotificationAreaService))
                        .AttributedPropertiesAutowired()
                        .AsImplementedInterfaces()
                        ;
                    #endregion
                })
                .ConfigureServices((hostContext, services) =>
                {

                })
            //.ConfigureLogging(loggerBuilder =>
            //{
            //    loggerBuilder.AddConsole
            //})
            ;

    }
}