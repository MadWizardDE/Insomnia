using Autofac;
using MadWizard.Insomnia.Service;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Power.Manager
{
    public class PowerManager : IPowerManager
    {
        public required ILogger<PowerManager> Logger { private get; init; }

        public PowerManager(WindowsService? service = null)
        {
            if (service != null)
            {
                service.PowerStatusChanged += PowerStatusChanged;
            }
        }

        public event EventHandler? Suspended;
        public event EventHandler? ResumeSuspended;

        private void PowerStatusChanged(object? sender, PowerBroadcastStatus status)
        {
            Logger.LogDebug($"{status}");

            switch (status)
            {
                case PowerBroadcastStatus.Suspend:
                    Suspended?.Invoke(this, EventArgs.Empty);
                    break;

                case PowerBroadcastStatus.ResumeSuspend:
                    ResumeSuspended?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }

        public void Suspend(bool hibernate = false)
        {
            Logger.LogDebug($"Received request to suspend the system to: {(hibernate ? "Hibernate" : "Standby")}");

            if (!SetSuspendState(false, false, false))
            {
                throw new Win32Exception();
            }
        }

        public void Shutdown(bool force = false)
        {
            Logger.LogDebug($"Received request to shutdown the system");

            var flags = EWX_SHUTDOWN;

            if (force)
            {
                flags |= EWX_FORCE;
            }

            if (!ExitWindowsEx(flags, 0))
            {
                throw new Win32Exception();
            }
        }

        public void Reboot(bool force = false)
        {
            Logger.LogDebug($"Received request to reboot the system");

            var flags = EWX_REBOOT;

            if (force)
            {
                flags |= EWX_FORCE;
            }

            if (!ExitWindowsEx(flags, 0))
            {
                throw new Win32Exception();
            }
        }

        IPowerRequest IPowerManager.CreateRequest(string reason)
        {
            return new PowerRequest(PowerRequestsType.SystemRequired, reason);
        }

        IEnumerator<IPowerRequest> IEnumerable<IPowerRequest>.GetEnumerator()
        {
            Dictionary<PowerRequestsType, List<PowerRequest>> requestsByType = [];

            using Process process = new()
            {
                StartInfo = new()
                {
                    FileName = @"powercfg",
                    Arguments = "-requests",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            if (process.Start())
            {
                using var reader = process.StandardOutput;

                string? line;
                string? requestTypeName = null;

                PowerRequest? request = null;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line = line.TrimEnd()))
                        continue;

                    if (!char.IsWhiteSpace(line[0]) && line.EndsWith(":"))
                    {
                        requestTypeName = line[..^1].Trim(); // Remove colon
                        request = null;
                    }
                    else if (requestTypeName != null && line.StartsWith('[') && line.Contains(']'))
                    {
                        int bracketEnd = line.IndexOf(']');
                        string callerType = line[1..bracketEnd];
                        string name = line[(bracketEnd + 1)..].Trim();

                        request = new PowerRequest(requestTypeName, callerType, name);

                        if (request.Type is PowerRequestsType type)
                        {
                            if (!requestsByType.TryGetValue(type, out var list))
                                requestsByType[type] = list = [];

                            list.Add(request);
                        }
                    }
                    else if (request != null)
                    {
                        request.Reason = line.Trim();
                    }
                }

                process.WaitForExit();

                if (requestsByType.TryGetValue(PowerRequestsType.SystemRequired, out var systemRequests))
                    foreach (var systemReuqest in systemRequests)
                        yield return systemReuqest;
            }
            else
            {
                throw new Exception("Failed to run powercfg /requests");
            }
        }

        #region API: Shutdown
        internal const int EWX_LOGOFF = 0x00000000;
        internal const int EWX_SHUTDOWN = 0x00000001;
        internal const int EWX_REBOOT = 0x00000002;
        internal const int EWX_FORCE = 0x00000004;
        internal const int EWX_POWEROFF = 0x00000008;
        internal const int EWX_FORCEIFHUNG = 0x00000010;

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool ExitWindowsEx(int flg, int rea);
        #endregion

        #region API: Standby-Modus
        [DllImport("powrprof.dll", CharSet = CharSet.Auto, ExactSpelling = true, SetLastError = true)]
        private static extern bool SetSuspendState(bool hiberate, bool forceCritical, bool disableWakeEvent);
        #endregion

    }
}
