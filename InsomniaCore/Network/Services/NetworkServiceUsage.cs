using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Network.Services
{
    public class NetworkServiceUsage(string name, int accessCount) : UsageToken
    {
        public string Name => name;
        public int AccessCount => accessCount;

        public override string ToString() => name;
    }
}
