using Autofac;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Power.Manager;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Power
{
    public class PowerRequestMonitor(PowerRequestMonitorConfig config, IPowerManager power) : IInspectable, IStartable
    {
        public required ILogger<PowerRequestMonitor> Logger { get; set; }

        void IStartable.Start()
        {
            Logger.LogDebug("Startup complete");
        }

        IEnumerable<UsageToken> IInspectable.Inspect(TimeSpan interval)
        {
            foreach (var request in config.Request)
            {
                if (power.HasMatchingRequest(request.Pattern))
                {
                    yield return new PowerRequestToken(request.Name);
                }
            }
        }
    }
}
