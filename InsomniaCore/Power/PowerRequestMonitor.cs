using Autofac;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Power.Manager;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

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
            if (config.Request.Any())
            {
                foreach (var request in power)
                    foreach (var info in config.Request)
                    {
                        bool matches = false;
                        if (info.Pattern.Matches(request.Name).Count > 0)
                            matches = true;
                        if (request.Reason != null && info.Pattern.Matches(request.Reason).Count > 0)
                            matches = true;

                        if (matches)
                        {
                            yield return new PowerRequestToken(info.Name);
                        }
                    }
            }
            else if (power.Any()) // if there aren't any requests configured, any power request will match
            {
                yield return new PowerRequestToken();
            }
        }
    }
}
