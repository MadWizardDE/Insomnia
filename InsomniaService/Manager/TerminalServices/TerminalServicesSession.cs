using MadWizard.Insomnia.Manager.Processes;
using MadWizard.Insomnia.Processes.Manager;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using static MadWizard.Insomnia.Processes.Manager.ProcessManager;

namespace MadWizard.Insomnia.Session.Manager
{
    public partial class TerminalServicesSession(uint id) : ISession
    {
        public uint Id => id;

        public string UserName => WTSInfo.UserName;
        public string DomainName => WTSInfo.Domain;
        public string WindowStationName => WTSInfo.WinStationName;

        public NTAccount? UserAccount => string.IsNullOrEmpty(UserName) ? null : new NTAccount(DomainName, UserName);

        public string? SID => (UserAccount?.Translate(typeof(SecurityIdentifier)) as SecurityIdentifier)?.Value;

        public string? ClientName => QuerySessionInformation<string>(id, WTS_INFO_CLASS.WTSClientName)?.NullIfWhiteSpace();

        public bool IsConnected => IsConsoleConnected || IsRemoteConnected;
        public bool IsConsoleConnected => WTSGetActiveConsoleSessionId() == Id;
        public bool IsRemoteConnected => WTSInfo.State == TerminalSessionState.Active && ClientName != null;

        //public bool IsUser => Principal.IsInRole(WindowsBuiltInRole.User);
        public bool IsUser => Principal.UserClaims.Any(c => c.Value.Contains(SID_GROUP_USERS));
        //public bool IsAdministrator => Principal.IsInRole(WindowsBuiltInRole.Administrator);
        public bool IsAdministrator => Principal.UserClaims.Any(c => c.Value.Contains(SID_GROUP_ADMINISTRATORS));

        public bool? IsLocked { get; internal set; }
        public virtual DateTime? LastInputTime // => null; // WTSInfo.LastInputTime; // doesn't work
        {
            get
            {
                using Process process = LaunchProcess(new(Environment.ProcessPath!, "test LastInputTime")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }, false);

                using (process.StandardOutput)
                {
                    string ticks = process.StandardOutput.ReadToEnd();

                    return new DateTime(long.Parse(ticks));
                }
            }
        }

        public required IEnumerable<IProcess> Processes { get; init; }

        public virtual async Task Lock()
        {
            throw new NotImplementedException();
        }
        public async Task ConnectToConsole()
        {
            await Task.Run(() =>
            {
                if (!WTSConnectSession(id, WTSGetActiveConsoleSessionId(), "", true))
                    throw new Win32Exception();
            });
        }
        public async Task Disconnect()
        {
            await Task.Run(() =>
            {
                if (!WTSDisconnectSession(0, id, true))
                    throw new Win32Exception();
            });
        }
        public async Task Logoff()
        {
            await Task.Run(() =>
            {
                if (!WTSLogoffSession(0, id, true))
                    throw new Win32Exception();
            });
        }

        public event EventHandler? Locked;
        public event EventHandler? Unlocked;
        public event EventHandler? Connected;
        public event EventHandler? Disconnected;

        internal WindowsIdentity Identity => new(Token);
        internal WindowsPrincipal Principal => new(Identity);

        public override string ToString()
        {
            var name = ClientName != null ? $@"{ClientName}\{UserName}" : UserName;

            var state = new List<string>([WTSInfo.State.ToString()]);

            if (IsConsoleConnected)
                state.Add("Console");
            if (IsRemoteConnected)
                state.Add("Remote");

            if (IsLocked.HasValue)
            {
                if (IsLocked.Value)
                    state.Add("Locked");
                else
                    state.Add("Unlocked");
            }

            return $"WTSUserSession[id={id}, name={name}, state={string.Join('|', state)}]";
        }

        public IProcess LaunchProcess(ProcessStartInfo info)
        {
            return new ProcessExt(LaunchProcess(info, false));
        }

        public Process LaunchProcess(ProcessStartInfo info, bool runAsSystem)
        {
            const int MAX_STARTUP_DELAY = 1000;

            var watch = Stopwatch.StartNew();

            while (true)
            {
                try
                {
                    if (runAsSystem)
                        return ProcessLauncher.CreateProcessInSession(info, Id);
                    else
                        return ProcessLauncher.CreateProcessInSession(info, Token);
                }
                catch (Win32Exception ex)
                {
                    if (watch.ElapsedMilliseconds < MAX_STARTUP_DELAY && ex.NativeErrorCode == 340)
                    {
                        // try again
                        Thread.Sleep(100);
                        continue;
                    }

                    throw;
                }
            }
        }
    }
}
