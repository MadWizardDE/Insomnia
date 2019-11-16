using Autofac.Features.OwnedInstances;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Sessions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service.Debug
{
    class TestWorker : BackgroundService
    {
        DebugParameterConfig _config;

        ISessionManager _sessionManager;
        Lazy<Owned<ISessionService<ITextMessageService>>> _lazyTextMessageService;
        Lazy<Owned<ISessionService<INotificationAreaService>>> _lazyTrayService;
        Lazy<Owned<ISessionService<ITestWTSService>>> _lazyWTSService;

        public TestWorker(ILogger<TestWorker> logger, InsomniaConfig config,
            ISessionManager sessionManager,
            Lazy<Owned<ISessionService<INotificationAreaService>>> lazyTrayService,
            Lazy<Owned<ISessionService<ITextMessageService>>> lazyTextMessageService,
            Lazy<Owned<ISessionService<ITestWTSService>>> lazyWTSService
            )
        {
            Logger = logger;

            _config = config.DebugParameters;

            _sessionManager = sessionManager;
            _lazyTrayService = lazyTrayService;
            _lazyTextMessageService = lazyTextMessageService;
            _lazyWTSService = lazyWTSService;
        }

        [Autowired]
        ILogger<TestWorker> Logger { get; set; }

        protected async override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_config != null)
            {
                if (_config.TestMessage != null)
                {
                    bool error = Logger.IsEnabled(LogLevel.Error);
                    bool warn = Logger.IsEnabled(LogLevel.Warning);
                    bool info = Logger.IsEnabled(LogLevel.Information);
                    bool debug = Logger.IsEnabled(LogLevel.Debug);

                    Logger.LogInformation("Retriving TextMessage-Service...");

                    var owned = _lazyTextMessageService.Value;

                    Logger.LogInformation("TextMessage-Service retrieved");

                    foreach (var serviceRef in owned.Value)
                        using ((serviceRef.Service as IServiceProxy).InvokeWithOptions(timeout: TimeSpan.MaxValue))
                        {
                            Logger.LogInformation("Sending TextMessage...");

                            try
                            {
                                await serviceRef.Service.ShowMessage(_config.TestMessage.Text, "Debug");
                            }
                            catch (TimeoutException)
                            {
                                Logger.LogError("TextMessage timed out!");
                            }

                            Logger.LogInformation("TextMessage sent");
                        }

                    await Task.Delay(5000);

                    Logger.LogInformation("Disposing TextMessage-Service...");

                    owned.Dispose();

                    Logger.LogInformation("TextMessage-Service disposed");
                }

                if (_config.TestConsoleConnect != null)
                {
                    const bool SCS = false;

                    if (SCS)
                    {
                        Logger.LogInformation("Retriving WTS-Service...");

                        var owned = _lazyWTSService.Value;

                        Logger.LogInformation("WTS-Service retrieved");

                        Logger.LogInformation("ConnectToConsole...");

                        owned.Value.SelectSession(_config.TestConsoleConnect.SID.Value).ConnectToConsole();

                        Logger.LogInformation("Disposing WTS-Service...");

                        await Task.Delay(5000);


                        Logger.LogInformation("Done.");

                        owned.Dispose();

                        Logger.LogInformation("WTS-Service disposed");
                    }
                    else
                    {
                        Logger.LogInformation($"Determining Source-/Target-Session...");

                        ISession source, target = _sessionManager.ConsoleSession;
                        if (_config.TestConsoleConnect.SID != null)
                            source = _sessionManager.FindSessionByID(_config.TestConsoleConnect.SID.Value);
                        else
                            source = _sessionManager.FindSessionByUserName(_config.TestConsoleConnect.User);

                        Logger.LogInformation($"Connecting SID={source.Id} to SID={target.Id}");

                        _sessionManager.ConnectSession(source, target);

                        await Task.Delay(5000);

                        using var tray = _lazyTrayService.Value;

                        await tray.Value.SelectSession(source.Id).ShowNotificationAsync(INotificationAreaService.NotifyMessageType.Info,
                            "Insomnia", $"Die Windows-Sitzung wurde erfolgreich von '{target.UserName}' zu '{source.UserName}' gewechselt.", timeout: 20000);
                    }
                }
            }

        }
    }
}
