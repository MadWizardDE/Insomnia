using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.Sessions
{
    [Serializable]
    public class IncarnationMessage : SystemMessage
    {
        public IncarnationMessage(int pid)
        {
            PID = pid;
        }

        public int PID { get; private set; }
    }
}