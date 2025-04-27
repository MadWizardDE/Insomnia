using Autofac;
using MadWizard.Insomnia.NetworkSession.Manager;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service
{
    internal class WindowsServiceInstaller : IStartable
    {
        public required ILogger<WindowsServiceInstaller> Logger { get; set; }

        public void Start()
        {
            // TODO: https://learn.microsoft.com/en-us/windows/win32/api/winsvc/nf-winsvc-createservicew
            // sc.exe: https://learn.microsoft.com/de-de/windows-server/administration/windows-commands/sc-create
        }
    }
}
