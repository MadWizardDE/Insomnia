using Autofac;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Power.Manager;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NLog.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            var filteredRequests = power.Where(ShouldMonitorRequest);

            if (config.Request.Any())
            {
                foreach (var request in filteredRequests)
                    foreach (var info in config.Request)
                        if (Matches(request, info.Pattern))
                            yield return new PowerRequestToken(info.Name);
            }
            else if (filteredRequests.Any()) // if there aren't any requests configured, any power request will match
            {
                yield return new PowerRequestToken();
            }
        }

        private bool ShouldMonitorRequest(IPowerRequest request)
        {
            foreach (var filter in config.RequestFilter)
                if (Matches(request, filter.Pattern))
                    return false;

            return true;
        }

        private static bool Matches(IPowerRequest request, Regex pattern)
        {
            if (pattern.Matches(request.Name).Count > 0)
                return true;
            if (request.Reason != null && pattern.Matches(request.Reason).Count > 0)
                return true;

            return false;
        }
    }
}
