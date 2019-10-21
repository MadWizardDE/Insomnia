using MadWizard.Insomnia.Service.Sessions;
using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Minion
{
    public interface IServiceMessageHandler
    {
        public void HandleMessage(ServiceMessage message);
    }
}
