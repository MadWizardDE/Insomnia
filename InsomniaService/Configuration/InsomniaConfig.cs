using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

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

        public UserInferfaceConfig UserInterface { get; set; }
        public SleepWatchConfig SleepWatch { get; set; }
        public RemoteControlConfig RemoteControl { get; set; }

        public LogSweeperConfig LogSweeper { get; set; }

        public LoggingConfig Logging { get; set; }
    }

    public class DebugParameterConfig
    {
        public int StartupDelay { get; set; }

        public TestMessageConfig TestMessage { get; set; }

        public class TestMessageConfig
        {
            public string Text { get; set; }
        }
    }

    public class UserInferfaceConfig
    {
        public TrayMenuConfig TrayMenu { get; set; }
        public MoonriseCommanderConfig MoonriseCommander { get; set; }
        public WindowCleanerConfig WindowCleaner { get; set; }

        public class TrayMenuConfig
        {

        }

        public class MoonriseCommanderConfig
        {

        }

        public class WindowCleanerConfig
        {
            public WindowCleanerConfig()
            {
                TitlePattern = new Dictionary<string, WindowTitlePattern>();
            }

            public IDictionary<string, WindowTitlePattern> TitlePattern { get; private set; }

            public class WindowTitlePattern
            {
                public string Name { get; set; }

                public string Text { get; set; }
            }
        }
    }

    public class SleepWatchConfig
    {
        public bool Log { get; set; }

        public ActivityDetectorConfig ActivityDetector { get; set; }
        public NetworkCommanderConfig NetworkCommander { get; set; }
        public AntiSleepWalkConfig AntiSleepWalk { get; set; }

        public class ActivityDetectorConfig
        {
            public int? IdleMax { get; set; }

            public PingHostConfig PingHost { get; set; }
            public PowerRequestConfig PowerRequests { get; set; }
            public RemoteDesktopConnectionConfig RemoteDesktopConnection { get; set; }
            public UserIdleConfig UserIdle { get; set; }
            public WakeOnLANConfig WakeOnLAN { get; set; }

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

            public class PowerRequestConfig
            {
                public bool KeepAlive { get; set; } = true;
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
        }

        public class NetworkCommanderConfig
        {
            bool _resolveIPAddress;

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
                get => _resolveIPAddress;

                set
                {
                    if (_resolveIPAddress != value)
                    {
                        _resolveIPAddress = value;

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
    }

    public class RemoteControlConfig
    {
        public DiscoveryType Discovery { get; set; } = DiscoveryType.None;
        public SecurityType Security { get; set; } = SecurityType.None;

        public enum DiscoveryType
        {
            None = 0,
            Broadcast
        }

        public enum SecurityType
        {
            None = 0
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

        public EventLogConfig EventLog { get; set; }

        public class EventLogConfig
        {
        }
    }
}