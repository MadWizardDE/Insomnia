using Autofac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Xml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using MadWizard.Insomnia.Configuration;
using Autofac.Extensions.DependencyInjection;
using System.IO;
using MadWizard.Insomnia.Service.Lifetime;
using System.Threading;
using MadWizard.Insomnia.Service.Sessions;
using MadWizard.Insomnia.Service.SleepWatch;
using MadWizard.Insomnia.Service.UI;
using MadWizard.Insomnia.Service.Debug;
using Microsoft.Extensions.Logging.EventLog;
using System.Reflection;
using NLog.Config;
using NLog.Targets;
using NLog.Extensions.Logging;
using System.Runtime.InteropServices;

namespace MadWizard.Insomnia.Service
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            Thread.Sleep(host.Services.GetService<InsomniaConfig>()?.DebugParameters?.StartupDelay ?? 0);

            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
                Host.CreateDefaultBuilder(args)
                    .UseInsomniaServiceLifetime()
                    .ConfigureAppConfiguration((ctx, builder) =>
                    {
                        builder.AddCustomXmlFile(Path.Combine(ctx.HostingEnvironment.ContentRootPath, "config.xml"));
                    })
                    .ConfigureLogging((ctx, loggerBuilder) =>
                    {
                        var config = ctx.Configuration.Get<InsomniaConfig>(opt => opt.BindNonPublicProperties = true);

                        if (config.Logging != null && config.Logging.LogLevel != LogLevel.None)
                        {
                            if (config.Logging.FileSystemLog != null)
                                ConfigureNLog(ctx.HostingEnvironment, loggerBuilder);

                            if (config.Logging.EventLog != null)
                                loggerBuilder.AddEventLog();

                            loggerBuilder.SetMinimumLevel(config.Logging.LogLevel);
                            loggerBuilder.AddConsole();
                        }
                    })

                    .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                    .ConfigureContainer<ContainerBuilder>((ctx, builder) =>
                    {
                        var config = ctx.Configuration.Get<InsomniaConfig>(opt => opt.BindNonPublicProperties = true);

                        builder.RegisterInstance(config);

                        builder.RegisterModule<SessionModule>();

                        if (config.SleepWatch != null)
                        {
                            builder.RegisterModule(new SleepWatchModule(config.SleepWatch));
                        }

                        if (config.UserInterface != null)
                        {
                            builder.RegisterModule(new UserInterfaceModule(config.UserInterface));
                        }

                        builder.RegisterType<LogFileSweeper>().AttributedPropertiesAutowired().AsImplementedInterfaces().SingleInstance();
                    })
                    .ConfigureServices((hostContext, services) =>
                    {
                        services.AddHostedService<TestWorker>();

                        services.Configure<EventLogSettings>(settings =>
                        {
                            if (string.IsNullOrEmpty(settings.SourceName))
                            {
                                settings.SourceName = hostContext.HostingEnvironment.ApplicationName;
                            }
                        });
                    })
                ;

        private static void ConfigureNLog(IHostEnvironment hostEnvironment, ILoggingBuilder loggingBuilder)
        {
            var config = new LoggingConfiguration();
            {
                var targetFile = new FileTarget("file")
                {
                    FileName = Path.Combine(hostEnvironment.ContentRootPath, "insomnia.log"),
                    Layout = "${longdate} ${pad:padding=5:inner=${level:uppercase=true}} ${logger:shortName=true} :: ${message}  ${exception}"
                };

                config.AddTarget(targetFile);
                config.AddRuleForAllLevels(targetFile);
            }

            loggingBuilder.AddNLog(config);
        }
    }
}