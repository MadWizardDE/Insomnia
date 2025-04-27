using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Network.Host;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Network.Services
{
    public abstract class NetworkService(string name) : Resource
    {
        public virtual bool IsHidden => false;

        internal string Name { get; private set; } = name;

        public int AccessCount { get; protected set; } = 0;
        public int AccessThreshold { get; set; } = 1;

        public event EventInvocation? Access;

        public abstract bool Accepts(Packet packet);

        public virtual void AccessBy(Packet packet, bool remember = true)
        {
            if (remember)
            {
                AccessCount++;
            }

            TriggerEvent(nameof(Access));
        }

        protected override IEnumerable<UsageToken> InspectResource(TimeSpan interval)
        {
            try
            {
                if (AccessCount >= AccessThreshold)
                {
                    yield return new NetworkServiceUsage(Name, AccessCount);
                }

                yield break;
            }
            finally
            {
                AccessCount = 0;
            }
        }

    }
}
