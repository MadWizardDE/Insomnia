using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia
{
    public abstract class UsageToken
    {
        public readonly ISet<UsageToken> Tokens = new HashSet<UsageToken>();
    }
}
