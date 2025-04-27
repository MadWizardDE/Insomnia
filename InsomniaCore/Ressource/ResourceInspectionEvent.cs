using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia
{
    public class ResourceInspectionEvent(string type) : Event(type)
    {
        public required IEnumerable<UsageToken> Tokens { get; init; }

        public required TimeSpan Duration { get; init; }
    }
}
