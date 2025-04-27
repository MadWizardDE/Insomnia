using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MadWizard.Insomnia.Network.Host;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Network.Services;
using Microsoft.Extensions.Logging;

namespace MadWizard.Insomnia.Network
{
    public class NetworkHostMonitor(NetworkMonitorConfig config) : ResourceMonitor<NetworkHost>
    {
        public required ILogger<NetworkHostMonitor> HostLogger { get; set; }

        public required Lazy<IEnumerable<IVirtualHostManager>> VirtualHostManagers { get; init; }

        public NetworkHost LocalHost { get; private set; } = new NetworkHost(new NetworkHostInfo() { Name = Dns.GetHostName() });

        protected void ConfigureHosts()
        {
            this.ConfigurePingHosts();

            this.ConfigureVirtualHosts();
            this.ConfigureLocalHost();
        }

        private void ConfigurePingHosts()
        {
            foreach (var host in config.PingHost)
            {
                this.StartTracking(new PingHost(host));

                HostLogger.LogDebug($"Monitoring host '{host.Name}' with ping");
            }
        }
        private void ConfigureVirtualHosts()
        {
            foreach (var hostConfig in config.VirtualHost)
            {
                try
                {
                    bool found = false;
                    foreach (var vmManager in VirtualHostManagers.Value)
                    {
                        if (vmManager.FindHostByName(hostConfig.Name) is IVirtualHost vHost)
                        {
                            var host = new VirtualHost(hostConfig, vHost);

                            this.StartTracking(host);

                            HostLogger.LogDebug($"Monitoring virtual host '{hostConfig.Name}'; services: {string.Join(", ", host)}");

                            found = true;

                            break;
                        }
                    }

                    if (!found)
                    {
                        HostLogger.LogWarning($"No virtual host found with name '{hostConfig.Name}'!");
                    }
                }
                catch (Exception ex)
                {
                    HostLogger.LogError(ex, $"Error while configuring virtual host '{hostConfig.Name}'");
                }
            }
        }
        private void ConfigureLocalHost()
        {
            foreach (var service in config.TCPService)
                this.LocalHost.StartTracking(new TCPService(service));
            foreach (var service in config.HTTPService)
                this.LocalHost.StartTracking(new HTTPService(service));

            this.StartTracking(this.LocalHost);

            HostLogger.LogDebug($"Monitoring local host; services: {string.Join(", ", this.LocalHost)}");
        }

        public bool FindHost(PhysicalAddress mac, out NetworkHost? foundHost)
        {
            foundHost = null;

            foreach (NetworkHost host in this)
            {
                if (mac.Equals(host.HostMAC))
                {
                    foundHost = host;
                    break;
                }
            }

            return false;
        }

        public bool FindHost(IPAddress ip, out NetworkHost? foundHost)
        {
            foundHost = null;

            foreach (NetworkHost host in this)
            {
                foreach (IPAddress hostIP in host.HostIPs)
                    if (ip.Equals(hostIP))
                    {
                        foundHost = host;
                        return true;
                    }
            }

            return false;
        }
    }
}
