using System;
using System.Collections.Generic;
using System.Text;
using static MadWizard.Insomnia.Service.Sessions.INotificationAreaService;

namespace MadWizard.Insomnia.Service.Sessions
{
    [Serializable]
    public class ConnectToConsoleMessage : UserMessage
    {
        public ConnectToConsoleMessage(UserInfo user)
        {
            User = user;
        }

        public UserInfo User { get; private set; }

    }
}