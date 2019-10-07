using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.Sessions
{
    [Serializable]
    public class SessionMinionConfig
    {
        public int Interval { get; internal set; }
    }
}