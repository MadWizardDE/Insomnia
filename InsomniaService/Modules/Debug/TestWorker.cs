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

        Lazy<Owned<ISessionService<ITextMessageService>>> _lazyTextMessageService;

        public TestWorker(ILogger<TestWorker> logger, InsomniaConfig config, Lazy<Owned<ISessionService<ITextMessageService>>> lazyTextMessageService)
        {
            Logger = logger;

            _config = config.DebugParameters;

            _lazyTextMessageService = lazyTextMessageService;
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
            }

        }
    }
}
