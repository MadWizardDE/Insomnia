using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;

namespace MadWizard.Insomnia.Service.Lifetime
{
    public interface IPowerEventHandler
    {
        void OnPowerEvent(PowerBroadcastStatus status);
    }
}