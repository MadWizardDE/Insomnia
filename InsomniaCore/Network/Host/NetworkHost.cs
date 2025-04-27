using Autofac;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Network.Services;
using PacketDotNet;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

using NetworkService = MadWizard.Insomnia.Network.Services.NetworkService;

namespace MadWizard.Insomnia.Network.Host
{
    public class NetworkHost : ResourceMonitor<NetworkService>
    {
        internal string Name { get; private set; }
        internal string HostName { get; private set; }

        internal ISet<IPAddress> HostIPs { get; init; }
        internal PhysicalAddress? HostMAC { get; set; }

        internal IPAddress HostIPv4Address => HostIPs.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).First();

        internal NetworkHost(NetworkHostInfo info)
        {
            HostName = Name = info.Name;

            HostMAC = info.PhysicalAddress;

            HostIPs = new HashSet<IPAddress>(Dns.GetHostEntry(HostName).AddressList);
            if (info.IPv4Address != null)
                HostIPs.Add(info.IPv4Address);
        }

        public virtual bool MatchService(Packet packet, out NetworkService? foundService)
        {
            foundService = null;

            foreach (NetworkService service in this)
                if (service.Accepts(packet))
                    return (foundService = service) != null;

            return false;
        }

        protected override bool ShouldInspectResource(NetworkService service) => !service.IsHidden;

        protected override IEnumerable<UsageToken> InspectResource(TimeSpan interval)
        {
            var token = new NetworkHostUsage(this);

            // summarize tokens
            foreach (var serviceToken in base.InspectResource(interval))
                if (serviceToken is NetworkServiceUsage service)
                    token.Tokens.Add(service);

            if (token.Tokens.Count() > 0)
                yield return token;
        }

    }

}
