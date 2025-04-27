using Autofac;
using MadWizard.Insomnia.Pipe;
using MadWizard.Insomnia.Pipe.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Minion
{
    public class SessionControlService
    {
        public SessionControlService(PipeMessageBroker broker)
        {
            broker.RegisterMessageHandler<LockMessage>(HandleLockMessage);
        }

        private void HandleLockMessage(LockMessage message)
        {
            LockWorkStation();
        }

        [DllImport("user32.dll")]
        private static extern bool LockWorkStation();
    }
}
