using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.Sessions
{
    [Serializable]
    public class StartupMessage : SystemMessage
    {
        internal StartupMessage(SessionMinionConfig config)
        {
            Config = config;
        }

        public SessionMinionConfig Config { get; private set; }

    }
}