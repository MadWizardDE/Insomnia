using MadWizard.Insomnia.Service;
using MadWizard.Insomnia.Service.Sessions;
using MadWizard.Insomnia.Tools;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
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

        [DllImport("Wtsapi32.dll")]
        static extern bool WTSConnectSession(UInt32 LogonId, UInt32 TargetLogonId, String pPassword, bool bWait);

        [Autowired]
        ILogger<SessionControlService> Logger { get; set; }

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

        public Task ConnectTo(int sessionID)
        {
            const bool TSCON = true;

            if (TSCON)
            {
                Logger.LogDebug("tscon");

                using (Process proc = new Process())
                {
                    SecureString password = new SecureString();
                    password.AppendChar('0');
                    password.AppendChar('n');
                    password.AppendChar('B');
                    password.AppendChar(':');
                    password.AppendChar(')');
                    password.AppendChar('7');
                    password.AppendChar('|');
                    password.AppendChar('l');


                    proc.StartInfo.FileName = "cmd";
                    //proc.StartInfo.Arguments = $"{sessionID}";
                    //proc.StartInfo.UserName = "Kevin";
                    //proc.StartInfo.Password = password;
                    proc.StartInfo.UseShellExecute = false;
                    //proc.StartInfo.CreateNoWindow = true;
                    //proc.StartInfo.RedirectStandardOutput = true;
                    //proc.StartInfo.RedirectStandardError = true;
                    proc.StartInfo.WorkingDirectory = @"C:\";
                    proc.Start();

                    //proc.WaitForExit();

                    //Logger.LogDebug("tscon finised = " + proc.ExitCode);

                    //string output = "";
                    //while (!proc.StandardOutput.EndOfStream)
                    //{
                    //    output += proc.StandardOutput.ReadLine();
                    //}

                    //string error = "";
                    //while (!proc.StandardError.EndOfStream)
                    //{
                    //    error += proc.StandardError.ReadLine();
                    //}

                    //Logger.LogDebug("Output = " + output);
                    //Logger.LogDebug("Error = " + error);
                };
            }
            else
            {
                if (!WTSConnectSession((uint)sessionID, (uint)Process.GetCurrentProcess().SessionId, "", true))
                {
                    int lastError = Marshal.GetLastWin32Error();

                    if (lastError > 0)
                        throw new Win32Exception(lastError);
                }

            }

            return Task.CompletedTask;
        }
    }
}