using System;
using System.Collections.Generic;
using System.Text;
using static MadWizard.Insomnia.Service.Sessions.INotificationAreaService;

namespace MadWizard.Insomnia.Service.Sessions
{
    [Serializable]
    public class ConfigureWakeOnLANMessage : UserMessage
    {
        public ConfigureWakeOnLANMessage(WakeTarget target)
        {
            Target = target;
        }

        public WakeTarget Target { get; private set; }

    }
}