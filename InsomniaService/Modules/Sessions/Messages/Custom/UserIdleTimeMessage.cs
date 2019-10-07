using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.Sessions
{
    [Serializable]
    public class UserIdleTimeMessage : UserMessage
    {
        public UserIdleTimeMessage(long time)
        {
            Time = time;
        }

        public long Time { get; private set; }

    }
}