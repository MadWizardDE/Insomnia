using MadWizard.Insomnia.Tools;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using static MadWizard.Insomnia.Configuration.SleepWatchConfig.ActivityDetectorConfig.ProcessActivityConfig;

namespace MadWizard.Insomnia.Configuration
{
    public class InsomniaConfig
    {
        public InsomniaConfig()
        {
            LogSweeper = new LogSweeperConfig();
            Logging = new LoggingConfig();
        }

        public int Interval { get; set; } = 60000;
        public int Port { get; set; } = 1473;

        public DebugParameterConfig DebugParameters { get; set; }

        public AutoLogoutConfig AutoLogout { get; set; }
        public UserInterfaceConfig UserInterface { get; set; }
        public SleepWatchConfig SleepWatch { get; set; }
        public LogSweeperConfig LogSweeper { get; set; }

        public LoggingConfig Logging { get; set; }
    }

    public class DebugParameterConfig
    {
        public int StartupDelay { get; set; }

        public TestMessageConfig TestMessage { get; set; }

        public TestConsoleConnectConfig TestConsoleConnect { get; set; }

        public class TestMessageConfig
        {
            public string Text { get; set; }
        }

        public class TestConsoleConnectConfig
        {
            public string User { get; set; }
            public int? SID { get; set; }
        }
    }

    public class AutoLogoutConfig
    {
        public AutoLogoutConfig()
        {
            User = new Dictionary<string, UserInfo>();
        }

        public int NotifyTimeout { get; set; } = 0;

        public IDictionary<string, UserInfo> User { get; private set; }

        public class UserInfo
        {
            public UserInfo()
            {
                LogoutException = new Dictionary<string, LogoutExceptionInfo>();
            }

            public string Name { get; set; }
            public int Timeout { get; set; } = 1000;
            public TimeSpan TimeoutSpan => TimeSpan.FromMilliseconds(Timeout);

            public IDictionary<string, LogoutExceptionInfo> LogoutException { get; private set; }

            public class LogoutExceptionInfo
            {
                public string Name { get; set; }
                public LogoutExceptionType Type { get; set; }
                public string Text { get; set; }

                public enum LogoutExceptionType
                {
                    REQUEST,
                    PROCESS
                }
            }
        }

    }

    public class UserInterfaceConfig
    {
        public TrayMenuConfig TrayMenu { get; set; }
        public MoonriseCommanderConfig MoonriseCommander { get; set; }
        public WindowManagerConfig WindowManager { get; set; }

        public class TrayMenuConfig
        {
            public SessionSwitchConfig SessionSwitch { get; set; }

            public class SessionSwitchConfig
            {
                public bool AllowAdministrator { get; set; } = true;
                public bool AllowUser { get; set; } = false;

                public bool AllowSelf { get; set; } = false;

                public bool AllowConsole { get; set; } = true;
                public bool AllowRemote { get; set; } = false;

                public int? KeepPrivileges { get; set; }
            }
        }

        public class MoonriseCommanderConfig
        {

        }

        public class WindowManagerConfig
        {
            public WindowManagerConfig()
            {
                CloseWindow = new Dictionary<string, CloseWindowConfig>();
                RestartProcess = new Dictionary<string, RestartProcessConfig>();
            }

            public int WaitTime { get; set; } = 2000;

            public IDictionary<string, CloseWindowConfig> CloseWindow { get; private set; }
            public IDictionary<string, RestartProcessConfig> RestartProcess { get; private set; }

            public class CloseWindowConfig
            {
                public string Name { get; set; }

                public string Title { get; set; }
            }

            public class RestartProcessConfig
            {
                public string Name { get; set; }

                public int After { get; set; }
                public int Timeout { get; set; } = 0;
            }

        }
    }

    public class SleepWatchConfig
    {
        public bool Log { get; set; }

        public SuspendState SuspendTo { get; set; } = SuspendState.SLEEP;

        public ActivityDetectorConfig ActivityDetector { get; set; }
        public NetworkCommanderConfig NetworkCommander { get; set; }
        public AntiSleepWalkConfig AntiSleepWalk { get; set; }
        public AntiGhostConfig AntiGhost { get; set; }

        public enum SuspendState
        {
            SLEEP,

            HIBERNATE
        }

        public class ActivityDetectorConfig
        {
            public int? IdleMax { get; set; }

            public PingHostConfig PingHost { get; set; }
            public PowerRequestConfig PowerRequests { get; set; }
            public ProcessActivityConfig ProcessActivity { get; set; }
            public RemoteDesktopConnectionConfig RemoteDesktopConnection { get; set; }
            public UserIdleConfig UserIdle { get; set; }
            public WakeOnLANConfig WakeOnLAN { get; set; }
            public ManualOverrideConfig ManualOverride { get; set; }
            public NetworkActivityConfig NetworkActivity { get; set; }
            public NetworkSessionsConfig NetworkSessions { get; set; }

            public class NetworkSessionsConfig
            {
                public bool IgnoreIdle { get; set; } = true;
            }

            public class NetworkActivityConfig
            {
                public string Interface { get; set; }

                public IDictionary<string, NetworkHostInfo> Host { get; private set; }
                public IDictionary<string, HyperVHostInfo> HyperVHost { get; private set; }

                public NetworkActivityConfig()
                {
                    Host = new Dictionary<string, NetworkHostInfo>();
                    HyperVHost = new Dictionary<string, HyperVHostInfo>();
                }

                public class NetworkHostInfo
                {
                    public string Name { get; set; }

                    public IDictionary<string, TCPServiceInfo> TCPService { get; private set; }

                    private string MAC { get; set; }
                    private string IPv4 { get; set; }

                    public PhysicalAddress PhysicalAddress => this.MAC != null ? PhysicalAddress.Parse(this.MAC) : null;
                    public IPAddress IPv4Address => this.IPv4 != null ? IPAddress.Parse(this.IPv4) : null;

                }

                public class HyperVHostInfo : NetworkHostInfo
                {
                    public RequestedState? OnAccess { get; set; } = RequestedState.Enabled;
                    public RequestedState? OnIdle { get; set; } = RequestedState.Offline;


                }

                public class TCPServiceInfo
                {
                    public string Name { get; set; }
                    public int Port { get; set; }
                    public int Threshold { get; set; } = 1;
                }

            }

            public class PingHostConfig
            {
                public PingHostConfig()
                {
                    Host = new Dictionary<string, HostInfo>();
                }

                public IDictionary<string, HostInfo> Host { get; private set; }

                public class HostInfo
                {
                    public string Name { get; set; }
                }
            }

            public class ProcessActivityConfig
            {

                public ProcessActivityConfig()
                {
                    Process = new Dictionary<string, ProcessInfo>();
                }

                public IDictionary<string, ProcessInfo> Process { get; private set; }

                public class ProcessInfo
                {
                    public string Name { get; set; }
                    public string ProcessName => Text;

                    public double Threshold { get; set; }
                    public bool WithChildren { get; set; }

                    private string Text { get; set; }
                }
            }


            public class PowerRequestConfig
            {
                public bool KeepAwake { get; set; } = true;
                public bool LogIfIdle { get; set; } = false;

                public PowerRequestConfig()
                {
                    Request = new Dictionary<string, RequestInfo>();
                }

                public IDictionary<string, RequestInfo> Request { get; private set; }

                public class RequestInfo
                {
                    public string Name { get; set; }

                    public IEnumerable<string> Strings => Text?.Split(',') ?? Enumerable.Empty<string>();

                    private string Text { get; set; }
                }
            }

            public class RemoteDesktopConnectionConfig
            {
            }

            public class UserIdleConfig
            {
            }

            public class WakeOnLANConfig
            {
            }

            public class ManualOverrideConfig
            {
                public bool AllowUser { get; set; } = false;
            }
        }

        public class NetworkCommanderConfig
        {
            bool _resolveIPAdress;

            public NetworkCommanderConfig()
            {
                Host = new Dictionary<string, HostInfo>();
                Network = new Dictionary<string, NetworkInfo>();

                ResolveIPAddress = false;
            }

            public WakePresence RequiredPresence { get; set; } = WakePresence.User;
            public WakeFrequency Wake { get; set; } = WakeFrequency.OneTime;

            public IDictionary<string, NetworkInfo> Network { get; private set; }
            public IDictionary<string, HostInfo> Host { get; private set; }

            #region Options
            public bool ResolveIPAddress
            {
                get => _resolveIPAdress;

                set
                {
                    if (_resolveIPAdress != value)
                    {
                        _resolveIPAdress = value;

                        ConfigChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            #endregion

            public event EventHandler ConfigChanged;

            public enum WakePresence
            {
                System = 0,
                User,
            }
            public enum WakeFrequency
            {
                Never = 0,

                OneTime,

                Continuous
            }

            public class NetworkInfo
            {
                public NetworkInfo()
                {
                    Host = new Dictionary<string, HostInfo>();
                }

                public string Name { get; set; }

                public string ID { get; set; }

                public IDictionary<string, HostInfo> Host { get; private set; }

                public MoonriseEnumeratorConfig MoonriseEnumerator { get; set; }

                public class MoonriseEnumeratorConfig
                {
                    public string URL { get; set; }
                }
            }
            public class HostInfo
            {
                private string _address;

                public HostInfo()
                {

                }

                public string Name { get; set; }

                public string Mode { get; set; }

                public PhysicalAddress PhysicalAddress => Address != null ? PhysicalAddress.Parse(Address.Replace(":", "").ToUpper()) : null;

                // Interne Properties:

                private string Address
                {
                    get => _address ?? Text;
                    set => _address = value;
                }
                private string Text { get; set; }

                public override string ToString()
                {
                    return $"HostInfo[{Name} -> {PhysicalAddress}]";
                }
            }
        }

        public class AntiSleepWalkConfig
        {
        }

        public class AntiGhostConfig
        {
        }
    }

    public class LogSweeperConfig
    {
        public int Count { get; set; } = 10;
    }

    public class LoggingConfig
    {
        public LogLevel LogLevel { get; set; } = LogLevel.Warning;
        public LogLevel LogLevelMinion { get; set; } = LogLevel.None;

        public FileSystemLogConfig FileSystemLog { get; set; }
        public EventLogConfig EventLog { get; set; }

        public class FileSystemLogConfig { }
        public class EventLogConfig { }
    }
}