using MadWizard.Insomnia.Pipe.Config;
using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Pipe.Messages
{
    public class NotificationAreaMessage : UserMessage
    {
        public NotificationAreaMessage(NotificationAreaConfig config)
        {
            Config = config;
        }

        public NotificationAreaConfig Config { get; set; }
    }

}
