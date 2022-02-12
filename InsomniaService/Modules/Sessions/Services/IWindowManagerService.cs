using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service.Sessions
{
    /*
     * Klasse zum Prozess-/Fensterputzen nach dem Standby / Anmeldung über RDP.
     */
    public interface IWindowManagerService
    {
        public Task CloseWindow(string title);

        public Task TerminateProcess(string processName, TimeSpan? maxRunningTime = null, TimeSpan? killTimeout = null, bool restart = false);
    }
}