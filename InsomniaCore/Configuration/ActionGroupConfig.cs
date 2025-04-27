using MadWizard.Insomnia.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Configuration
{
    public class ActionGroupConfig
    {
        public IList<ActionConfig> Action { get; private set; } = [];
    }

    public class ActionConfig
    {
        public required string Name { get; set; }

        public required Interval Delay { get; set; }

        public required NamedAction Text { get; set; }
    }
}
