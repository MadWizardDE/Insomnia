using Autofac;
using Autofac.Extensions.DependencyInjection;
using MadWizard.Insomnia;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Network.Manager;
using MadWizard.Insomnia.NetworkSession.Manager;
using MadWizard.Insomnia.Power.Manager;
using MadWizard.Insomnia.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration.Xml;
using NLog.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;
using NLog;
using MadWizard.Insomnia.Session.Manager;
using MadWizard.Insomnia.Processes.Manager;
using MadWizard.Insomnia.Logging;
using MadWizard.Insomnia.Test;
using MadWizard.Insomnia.Service.Actions;
using MadWizard.Insomnia.Service.Configuration;
using CommandLine;
using MadWizard.Insomnia.Service.Options;
using MadWizard.Insomnia.Service.Configuration.Builder;
using System.ServiceProcess;

//await MadWizard.Insomnia.Test.Debugger.UntilAttached();

LogManager.Setup().SetupExtensions(ext => ext.RegisterLayoutRenderer<SleepTimeLayoutRenderer>("sleep-duration"));

if (Process.GetCurrentProcess().IsWindowsService())
{
    var dir = new FileInfo(Assembly.GetExecutingAssembly().Locati‌​on).Directory!;

    Directory.SetCurrentDirectory(dir.FullName);

    var logs = Path.Combine(Directory.GetCurrentDirectory(), "logs");

    Directory.CreateDirectory(logs);

    File.Delete(@"logs/error.log");
}

bool isRunningAsService = Process.GetCurrentProcess().IsWindowsService();

string configDir = Path.Combine(Directory.GetCurrentDirectory(), "config");
string configFileName = "config.xml";
string configFilePath = Path.Combine(configDir, configFileName);

ServiceController service = new("InsomniaService");

switch (Parser.Default.ParseArguments<TestOptions, ServiceOptions>(args).Value)
{
    case ServiceOptions opts when opts.Command == "start":
        service.Start();
        service.WaitForStatus(ServiceControllerStatus.Running, opts.Timeout);
        return 0;
    case ServiceOptions opts when opts.Command == "stop":
        service.Stop();
        service.WaitForStatus(ServiceControllerStatus.Stopped, opts.Timeout);
        return 0;

    case TestOptions opts when opts.Command == "LastInputTime":
        long ticks = UserInput.LastInputTimeTicks;
        Console.Write($"{ticks}");
        return 0;
}

IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
    .UseContentRoot(Directory.GetCurrentDirectory())

    .ConfigureAppConfiguration((ctx, builder) =>
    {
        builder.SetBasePath(configDir);

        builder.AddCustomXmlFile(configFileName, optional: !isRunningAsService, reloadOnChange: true);

        builder.AddCommandLine(args);
    })

    .ConfigureLogging((ctx, builder) =>
    {
        builder.ClearProviders();
        builder.AddConsole();
        builder.AddNLog();
    })

    .UseServiceProviderFactory(new AutofacServiceProviderFactory()).ConfigureContainer<ContainerBuilder>((ctx, builder) =>
    {
        var config = ctx.Configuration.Get<InsomniaConfig>(opt => opt.BindNonPublicProperties = true);

        builder.Register(_ => ctx.Configuration).As<IConfiguration>();

        if (config != null)
        {
            // Implementing Platform-Managers
            builder.RegisterType<PowerManager>()
                .AsImplementedInterfaces()
                .SingleInstance()
                .AsSelf();
            builder.RegisterType<ProcessManager>()
                .AsImplementedInterfaces()
                .SingleInstance()
                .AsSelf();
            builder.RegisterType<HyperVManager>()
                .AsImplementedInterfaces()
                .SingleInstance()
                .AsSelf();

            // Implementing Network-Session-Managers
            builder.RegisterType<CIMNetworkSessionManager>()
                .AsImplementedInterfaces()
                .SingleInstance()
                .AsSelf();
            builder.RegisterType<CIMNetworkShareManager>()
                .AsImplementedInterfaces()
                .SingleInstance()
                .AsSelf();
            builder.RegisterType<CIMNetworkFileManager>()
                .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies)
                .AsImplementedInterfaces()
                .SingleInstance()
                .AsSelf();

            builder.RegisterType<TerminalServicesManager>()
                .PropertiesAutowired()
                .AsImplementedInterfaces()
                .SingleInstance()
                .AsSelf();


            // Add Actions
            builder.RegisterType<CommandExecutor>()
                .AsImplementedInterfaces()
                .SingleInstance()
                .As<Actor>();


            if (isRunningAsService)
            {
                // Attaching Service Control
                builder.RegisterType<WindowsService>()
                    .AsImplementedInterfaces()
                    .SingleInstance()
                    .AsSelf();

                builder.RegisterModule(new CoreModule(config));

                builder.RegisterPluginModules<PluginModule>();
            }
            else
            {
                switch (Parser.Default.ParseArguments<TestOptions, ConfigOptions>(args).Value)
                {
                    case ConfigOptions opts:
                        switch (opts.Action)
                        {
                            case "init":
                                builder.RegisterType<InitialConfigurationBuilder>()
                                    .WithParameter("iniFilePath", opts.IniFilePath)
                                    .WithParameter("configFilePath", configFilePath)
                                    .AsImplementedInterfaces()
                                    .SingleInstance()
                                    .AsSelf();
                                break;
                            case "read":
                                builder.RegisterType<InitialConfigurationReader>()
                                    .WithParameter("configFilePath", configFilePath)
                                    .WithParameter("iniFilePath", opts.IniFilePath)
                                    .AsImplementedInterfaces()
                                    .SingleInstance()
                                    .AsSelf();
                                break;
                        }

                        // Add InitialConfigurators
                        builder.RegisterType<NetworkMonitorConfigurator>()
                            .AsImplementedInterfaces()
                            .SingleInstance();
                        builder.RegisterType<SessionMonitorConfigurator>()
                            .AsImplementedInterfaces()
                            .SingleInstance();
                        builder.RegisterType<PowerRequestMonitorConfigurator>()
                            .AsImplementedInterfaces()
                            .SingleInstance();
                        builder.RegisterType<NetworkSessionMonitorConfigurator>()
                            .AsImplementedInterfaces()
                            .SingleInstance();


                        builder.RegisterPluginModules<ConfigPluginModule>();

                        break;

                    default:
                        throw new Exception("Invalid command line arguments.");
                }
            }

        }
    })
            
    .ConfigureServices((ctx, services) =>
    {
        services.Configure<InsomniaConfig>(ctx.Configuration, opt => opt.BindNonPublicProperties = true);

        //services.AddHostedService<Test>();
    })
;

TaskScheduler.UnobservedTaskException += (sender, args) =>
{
    File.AppendAllText(@"logs/error.log", $"Unobserved Task Exception: {args.Exception}");
    args.SetObserved(); // Prevents app crash
};

try
{
    var host = CreateHostBuilder(args).Build();

    host.Run();

    return 0;
}
catch (Exception e)
{
    File.WriteAllText(@"logs/error.log", e.ToString());

    return 1;
}
