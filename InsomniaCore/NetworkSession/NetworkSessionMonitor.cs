using Autofac;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.NetworkSession.Manager;
using MadWizard.Insomnia.Session;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.NetworkSession
{
    public class NetworkSessionMonitor(NetworkSessionMonitorConfig config, INetworkSessionManager manager) : IInspectable, IStartable
    {
        public required ILogger<NetworkSessionMonitor> Logger { get; set; }

        void IStartable.Start()
        {
            config.ToString();

            Logger.LogDebug("Startup complete");
        }

        IEnumerable<UsageToken> IInspectable.Inspect(TimeSpan interval)
        {
            string localhostName = Dns.GetHostName();

            // TODO Filtern

            foreach (var session in manager)
            {
                if (session.ComputerName == localhostName)
                    continue;
                if (session.IdleTime > interval)
                    continue;
                if (session.NumberOfOpenFiles == 0)
                    continue;

                var token = new NetworkSessionUsage(session.IPAddress, session.ComputerName, session.UserName, session.NumberOfOpenFiles);

                // TODO coalescing
                //foreach (var t in check.Parent?.Tokens ?? [])
                //    if (t is SessionUsageToken s && s.MatchesNetworkSession(token))
                //    {
                //        s.HasNetworkSession = true;

                //        goto skip;
                //    }

                yield return token;

                skip:;
            }
        }
    }
}
