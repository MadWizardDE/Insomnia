using MadWizard.Insomnia.Pipe.Config;
using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Pipe.Messages
{
    public class StartupMessage : SystemMessage
    {
        public StartupMessage(MinionConfig config)
        {
            Config = config;
        }

        public MinionConfig Config { get; set; }
    }
}
