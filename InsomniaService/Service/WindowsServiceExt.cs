using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;

namespace MadWizard.Insomnia.Service
{
    public static class WindowsServiceExt
    {
        public static bool IsWindowsService(this Process process)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return false;

            return process.SessionId == 0;
        }


    }
}