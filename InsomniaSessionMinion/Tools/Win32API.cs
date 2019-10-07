using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace MadWizard.Insomnia.Minion.Tools
{
    static class Win32API
    {
        [DllImport("user32.dll")]
        internal static extern bool SetProcessDPIAware();
    }
}