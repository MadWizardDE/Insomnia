using MadWizard.Insomnia.Service.Sessions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace MadWizard.Insomnia.Minion.Services
{
    class TestWTSService : ITestWTSService
    {
        [DllImport("Kernel32.dll")]
        static extern UInt32 WTSGetActiveConsoleSessionId();

        [DllImport("Wtsapi32.dll")]
        static extern bool WTSDisconnectSession(IntPtr hServer, uint SessionId, bool bWait);

        [DllImport("Wtsapi32.dll")]
        static extern bool WTSConnectSession(
            UInt32 LogonId,
            UInt32 TargetLogonId,
            String pPassword,
            bool bWait);

        [DllImport("Kernel32.dll")]
        static extern UInt32 GetCurrentProcessId();

        [DllImport("Kernel32.dll")]
        static extern UInt32 ProcessIdToSessionId(UInt32 dwProcessId, ref UInt32 pSessionId);

        public void ConnectToConsole()
        {
            string sourceUser = null, sourcePassword = "";

            UInt32 sourceSessionId = GetCurrentSessionId();
            UInt32 targetSessionId = WTSGetActiveConsoleSessionId();

            WTSDisconnectSession(IntPtr.Zero, sourceSessionId, true);

            if (!WTSConnectSession(sourceSessionId, targetSessionId, sourcePassword, true))
            {
                int lastError = Marshal.GetLastWin32Error();

                throw new Win32Exception(lastError);
            }
        }

        static UInt32 GetCurrentSessionId()
        {
            UInt32 sessionId = 0;
            ProcessIdToSessionId(GetCurrentProcessId(), ref sessionId);
            return sessionId;
        }
    }
}