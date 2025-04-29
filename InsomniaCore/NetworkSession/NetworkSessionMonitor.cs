using Autofac;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.NetworkSession.Manager;
using MadWizard.Insomnia.Power.Manager;
using MadWizard.Insomnia.Session;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.NetworkSession
{
    public class NetworkSessionMonitor(NetworkSessionMonitorConfig config, INetworkSessionManager manager) : IInspectable, IStartable
    {
        private static readonly bool SHOW_SHARE_USAGE = false;

        public required ILogger<NetworkSessionMonitor> Logger { private get; init; }

        void IStartable.Start()
        {
            Logger.LogDebug($"Startup complete; {config.FilterRule.Count()} filter rules found.");
        }

        IEnumerable<UsageToken> IInspectable.Inspect(TimeSpan interval)
        {
            foreach (var session in manager)
            {
                if (session.IdleTime > interval)
                    continue;

                var filteredFiles = session.OpenFiles.Where(file => !ShouldFilterFile(file, config.FilterRule));

                if (filteredFiles.Any())
                {
                    if (SHOW_SHARE_USAGE)
                    {
                        var shares = new HashSet<INetworkShare>();
                        foreach (var file in filteredFiles)
                            shares.Add(file.Share);

                        foreach (var share in shares)
                            yield return new NetworkSessionUsage(session, share);
                    }
                    else
                    {
                        yield return new NetworkSessionUsage(session);
                    }
                }
            }
        }

        private static bool ShouldFilterFile(INetworkFile file, IEnumerable<NetworkSessionFilterRule> filters)
        {
            foreach (var rule in filters)
                if (ShouldFilterFile(file, rule))
                    return true;

            return false;
        }

        private static bool ShouldFilterFile(INetworkFile file, NetworkSessionFilterRule rule)
        {
            int match = 0, mismatch = 0;

            if (rule.UserName is string ruleUserName && file.Session.UserName is string userName)
            {
                var _ = string.Equals(ruleUserName, userName, StringComparison.InvariantCultureIgnoreCase) ? match++ : mismatch++;
            }

            if (rule.ClientName is string ruleClientName && file.Session.Client.Name is string clientName)
            {
                var _ = string.Equals(ruleClientName, clientName, StringComparison.InvariantCultureIgnoreCase) ? match++ : mismatch++;
            }

            if (rule.ClientIPAddress is IPAddress ruleIPAddress && file.Session.Client.Address is IPAddress address)
            {
                var _ = ruleIPAddress.Equals(address) ? match++ : mismatch++;
            }

            if (rule.ShareName is string ruleShareName && file.Share.Name is string shareName)
            {
                var _ = string.Equals(ruleShareName, shareName, StringComparison.InvariantCultureIgnoreCase) ? match++ : mismatch++;
            }

            if (rule.FilePathPattern is Regex pattern)
            {
                var _ = pattern.IsMatch(file.Path) ? match++ : mismatch++;
            }

            return rule.Type switch
            {
                FilterType.Include => mismatch != 0,
                FilterType.Exclude => match != 0,

                _ => throw new NotImplementedException("unknown filter rule type"),
            };
        }
    }
}
