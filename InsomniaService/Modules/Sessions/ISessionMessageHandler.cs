using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.Sessions
{
    interface ISessionMessageHandler<T> where T : UserMessage
    {
        void Handle(ISession session, T message);
    }
}