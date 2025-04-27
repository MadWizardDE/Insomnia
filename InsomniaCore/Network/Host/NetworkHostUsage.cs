using MadWizard.Insomnia.Network.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Network.Host
{
    public class NetworkHostUsage(NetworkHost host) : UsageToken
    {
        public string Name => host.Name;

        public NetworkHost Host => host;

        public IList<NetworkServiceUsage> Services => [.. Tokens.OfType<NetworkServiceUsage>()];

        public override string ToString() => Name + (Services != null ? ":" + string.Join(':', Services) : string.Empty);
    }
}
