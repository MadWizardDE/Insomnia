using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Minion
{
    interface IUserInterface
    {
        public void SendAction(Action action);
        public Task SendActionAsync(Action action);

        public void PostAction(Action action);
    }
}