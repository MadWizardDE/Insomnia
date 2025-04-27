using Autofac;
using MadWizard.Insomnia.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Timers;

namespace MadWizard.Insomnia
{
    public class SystemUsageInspector(IOptionsMonitor<InsomniaConfig> config, SystemMonitor system) : IHostedService, IStartable
    {
        public required ILogger<SystemUsageInspector> Logger { protected get; init; }

        public TimeSpan? Interval => config.CurrentValue.Timeout?.Duration;

        public DateTime LastTime { get; private set; } = DateTime.Now;

        public DateTime? NextTime => Interval.HasValue ? LastTime + Interval.Value : null;

        public UsageToken[] LastTokens { get; private set; } = [];

        public event EventHandler? Inspected;

        private System.Timers.Timer? _timer;

        void IStartable.Start()
        {
            config.OnChange(ConfigurationChanged);

            system.Inspection += LogInspectionResult;
        }

        async Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            if (Interval.HasValue)
            {
                Logger.LogDebug($"Checking resources every {Interval.Value}");

                ConfigureTimer(Interval);
            }
        }

        private void ConfigureTimer(TimeSpan? timeout)
        {
            LastTime = DateTime.Now;

            _timer?.Stop();
            _timer = null;

            if (timeout.HasValue)
            {
                _timer = new(timeout.Value);
                _timer.AutoReset = true;
                _timer.Elapsed += Timer_Elapsed;
                _timer.Start();
            }

            Inspected?.Invoke(this, EventArgs.Empty);
        }

        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            InspectNow();
        }

        public void InspectNow()
        {
            _timer?.Stop();

            try
            {
                LastTokens = [.. system.Inspect(DateTime.Now - LastTime)];
            }
            catch (Exception e)
            {
                Logger.LogError(e, "ERROR");
            }
            finally
            {
                ConfigureTimer(Interval);
            }
        }

        async Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            ConfigureTimer(null);
        }

        private void ConfigurationChanged(InsomniaConfig config)
        {
            if (config.Timeout != null)
            {
                Logger.LogDebug($"Configuration changed. Checking usage every {config.Timeout}.");
            }
            else
            {
                Logger.LogDebug($"Configuration changed.");
            }

            ConfigureTimer(config.Timeout?.Duration);
        }

        private void LogInspectionResult(Event eventObj)
        {
            if (eventObj is ResourceInspectionEvent usage)
            {
                Logger.LogInformation("{tokens} [{time} ms]", usage.Tokens, usage.Duration.TotalMilliseconds);
            }
        }
    }
}
