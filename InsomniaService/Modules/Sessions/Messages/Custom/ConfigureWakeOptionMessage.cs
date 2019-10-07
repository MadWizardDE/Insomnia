using System;
using System.Collections.Generic;
using System.Text;
using static MadWizard.Insomnia.Service.Sessions.INotificationAreaService;

namespace MadWizard.Insomnia.Service.Sessions
{
    [Serializable]
    public class ConfigureWakeOptionMessage : UserMessage
    {
        public ConfigureWakeOptionMessage(WakeOption option)
        {
            Option = option;
        }

        public WakeOption Option { get; private set; }

    }
}