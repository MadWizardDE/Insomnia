using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Power
{
    internal class PowerRequestToken(string name) : UsageToken
    {
        public override string ToString() => $"(({name}))";
    }
}
