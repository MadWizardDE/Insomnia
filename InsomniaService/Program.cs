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
                .ConfigureAppConfiguration(builder =>
                {
                    builder.AddCustomXmlFile(@"C:\Users\Kevin\Source\Repos\Insomnia\config.xml");
                })
                .ConfigureLogging((ctx, loggerBuilder) =>
                {
                    var config = ctx.Configuration.Get<InsomniaConfig>(opt => opt.BindNonPublicProperties = true);

                    if (config.Logging != null)
                    {
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
                        builder.RegisterModule<SleepWatchModule>();
                    }

                    if (config.UserInterface != null)
                    {
                        builder.RegisterModule<UserInterfaceModule>();
                    }

                    builder.RegisterType<LogFileSweeper>().AttributedPropertiesAutowired().AsImplementedInterfaces().SingleInstance();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<TestWorker>();
                })
            ;
    }
}