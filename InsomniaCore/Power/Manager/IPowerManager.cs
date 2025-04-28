using MadWizard.Insomnia.NetworkSession.Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Power.Manager
{
    public interface IPowerManager : IIEnumerable<IPowerRequest>
    {
        public event EventHandler Suspended;
        public event EventHandler ResumeSuspended;

        public void Suspend(bool hibernate = false);
        public void Shutdown(bool force = false);
        public void Reboot(bool force = false);

        public IPowerRequest CreateRequest(string reason);
    }
}
