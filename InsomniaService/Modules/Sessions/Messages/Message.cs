using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.Sessions
{
    [Serializable]
    public abstract class Message
    {
        public static readonly string PIPE_NAME = "InsomniaPipe";

        protected Message()
        {

        }
    }

    [Serializable]
    public abstract class SystemMessage : Message
    {

    }

    [Serializable]
    public abstract class UserMessage : Message
    {

    }
}