using MadWizard.Insomnia.Service.Sessions;
using MadWizard.Insomnia.Tools;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Minion.Services
{
    class SessionControlService : ISessionControlService
    {
        [DllImport("user32.dll")]
        static extern bool LockWorkStation();

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

        public Task Lock()
        {
            LockWorkStation();

            return Task.CompletedTask;
        }

        public Task Logoff(bool force = false)
        {
            if (force)
                ExitWindowsEx(0 | 0x00000004, 0);
            else
                ExitWindowsEx(0, 0);

            return Task.CompletedTask;
        }
    }
}