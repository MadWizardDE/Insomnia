using MadWizard.Insomnia.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service.Duo.Configuration
{
    public class DuoStreamMonitorConfig
    {
        public required string ServiceName { get; set; } = "DuoService";

        //public int ManagerPort { get; set; } = 38299;
        public Interval ManagerInterval { get; set; } = new Interval(TimeSpan.FromSeconds(5));

        public NamedAction? OnInstanceDemand { get; set; }

        public ScheduledAction? OnInstanceIdle { get; set; }

        public ScheduledAction? OnInstanceLogin{ get; set; }
        public ScheduledAction? OnInstanceStarted { get; set; }
        public ScheduledAction? OnInstanceStopped { get; set; }
        public ScheduledAction? OnInstanceLogoff { get; set; }

        public IList<DuoInstanceInfo> Instance { get; private set; } = [];

        public bool UseFallback { get; set; } = false;

        internal DuoInstanceInfo? this[string name] => Instance.FirstOrDefault(i => i.Name == name);
    }
}
