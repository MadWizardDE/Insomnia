using Autofac;
using Microsoft.Extensions.Configuration;
using Autofac.Extensions.DependencyInjection;
using MadWizard.Insomnia.Minion.Services;
using MadWizard.Insomnia.Minion.Tools;
using MadWizard.Insomnia.Service;
using MadWizard.Insomnia.Service.Sessions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using NamedPipeWrapper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows.Forms;
using NLog.Config;
using NLog.Targets;

using Message = MadWizard.Insomnia.Service.Sessions.Message;

namespace MadWizard.Insomnia.Minion
{
    static class Program
    {
        static void Main(string[] args)
        {
            /* Hilft gegen unscharfe UI-Elemente */
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
            Host.CreateDefaultBuilder()

                .ConfigureLogging((ctx, loggingBuilder) =>
                {
                    loggingBuilder.ClearProviders();

                    if (boot.LogLevel != LogLevel.None)
                    {
                        var config = new LoggingConfiguration();
                        {
                            FileInfo minionEXE = new FileInfo(Assembly.GetExecutingAssembly().Location);

                            var targetFile = new FileTarget("file")
                            {
                                FileName = Path.Combine(minionEXE.DirectoryName, "minion.log"),
                                Layout = "${longdate} ${level} ${message}  ${exception}"
                            };

                            config.AddTarget(targetFile);
                            config.AddRuleForAllLevels(targetFile);
                        }

                        loggingBuilder.AddNLog(config);
                    }

                    loggingBuilder.SetMinimumLevel(boot.LogLevel);
                })

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
                        .AsImplementedInterfaces()
                        .SingleInstance()
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
            ;

    }
}