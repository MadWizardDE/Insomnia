using MadWizard.Insomnia.Service.Sessions;
using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Minion
{
    interface IUserMessenger
    {
        public void SendMessage(UserMessage message);
    }
}