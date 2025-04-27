using MadWizard.Insomnia.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Configuration
{
    public class InsomniaConfig
    {
        public Interval? Timeout { get; set; }

        public DelayedAction? OnIdle { get; set; }
        public NamedAction? OnUsage { get; set; } = new NamedAction("sleepless");

        public SessionMonitorConfig? SessionMonitor { get; set; }
        public ProcessMonitorConfig? ProcessMonitor { get; set; }
        public IList<NetworkMonitorConfig> NetworkMonitor { get; private set; } = [];
        public NetworkSessionMonitorConfig? NetworkSessionMonitor { get; set; }
        public PowerRequestMonitorConfig? PowerRequestMonitor { get; set; }

        public IList<ActionGroupConfig> ActionGroup { get; private set; } = [];

    }
}
