using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service.Sessions
{
    /*
     * Klasse zum Fensterputzen nach dem Standby / Anmeldung über RDP.
     */
    public interface IWindowCleanerService
    {
        public Task Wipe(string title);
    }
}