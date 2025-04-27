using MadWizard.Insomnia.Configuration;
using System.Net.NetworkInformation;

namespace MadWizard.Insomnia.Network.Host
{
    public class PingHost(NetworkHostInfo info) : NetworkHost(info)
    {
        protected override IEnumerable<UsageToken> InspectResource(TimeSpan interval)
        {
            using Ping ping = new();

            PingReply reply = ping.Send(info.Name);

            if (reply.Status == IPStatus.Success)
            {
                yield return new NetworkHostUsage(this);
            }
        }
    }

}
