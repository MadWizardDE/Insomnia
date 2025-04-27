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
            return new PowerRequest(reason);
        }

        bool IPowerManager.HasMatchingRequest(Regex pattern)
        {
            var raw = PowerQueryRequestsRaw();

            return pattern.Matches(raw).Count > 0;
        }

        private class PowerRequest : IPowerRequest
        {
            private readonly nint _request;

            internal PowerRequest(string reason)
            {
                // Create new power request.
                POWER_REQUEST_CONTEXT context = new()
                {
                    Flags = POWER_REQUEST_CONTEXT_SIMPLE_STRING,
                    Version = POWER_REQUEST_CONTEXT_VERSION,
                    SimpleReasonString = Reason = reason
                };

                _request = PowerCreateRequest(ref context);

                if (_request == nint.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                if (!PowerSetRequest(_request, PowerRequestType.PowerRequestSystemRequired))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }

            public string Reason { get; private set; }

            public void Dispose()
            {
                if (!PowerClearRequest(_request, PowerRequestType.PowerRequestSystemRequired))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
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

        #region API: Power-Requests
        private static string PowerQueryRequestsRaw()
        {
            Process process = new()
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

            process.Start();

            return process.StandardOutput.ReadToEnd();
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern nint PowerCreateRequest(ref POWER_REQUEST_CONTEXT Context);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool PowerSetRequest(nint PowerRequestHandle, PowerRequestType RequestType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool PowerClearRequest(nint PowerRequestHandle, PowerRequestType RequestType);

        private const int POWER_REQUEST_CONTEXT_VERSION = 0;
        private const int POWER_REQUEST_CONTEXT_SIMPLE_STRING = 0x1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct POWER_REQUEST_CONTEXT
        {
            public uint Version;
            public uint Flags;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string SimpleReasonString;
        }

        private enum PowerRequestType
        {
            PowerRequestDisplayRequired = 0,
            PowerRequestSystemRequired,
            PowerRequestAwayModeRequired,
            PowerRequestExecutionRequired
        }
        #endregion
    }
}
