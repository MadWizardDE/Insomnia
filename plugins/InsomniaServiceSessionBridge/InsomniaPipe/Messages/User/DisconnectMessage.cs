using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Pipe.Messages
{
    public class DisconnectMessage : UserMessage
    {
        public DisconnectMessage(uint sessionID)
        {
            SessionID = sessionID;
        }

        public uint SessionID { get; set; }
    }
}
