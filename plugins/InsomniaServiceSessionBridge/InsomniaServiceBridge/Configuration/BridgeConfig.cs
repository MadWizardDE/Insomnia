using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Pipe.Config;
using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.Bridge.Configuration
{
    public class BridgeConfig
    {
        public Interval? Timeout { get; set; }

        public BridgedSessionManagerConfig? SessionMonitor { get; set; }

    }
}
