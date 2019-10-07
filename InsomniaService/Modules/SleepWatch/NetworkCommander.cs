using Autofac;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Lifetime;
using MadWizard.Insomnia.Service.Sessions;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAPICodePack.Net;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Timers;

using static MadWizard.Insomnia.Configuration.SleepWatchConfig;
using static MadWizard.Insomnia.Configuration.SleepWatchConfig.NetworkCommanderConfig;

namespace MadWizard.Insomnia.Service.SleepWatch
{
    internal class NetworkCommander : IStartable, IPowerEventHandler, IDisposable
    {
        InsomniaConfig _config;
        NetworkCommanderConfig _configCommander;

        SessionManager _sessionManager;

        Timer _wakeTimer;

        IDictionary<string, IWakeMode> _wakeModes;
        IDictionary<string, Network> _wakeNetworks;

        public NetworkCommander(InsomniaConfig config, SessionManager sessionManager, IEnumerable<IWakeMode> modes)
        {
            _config = config;
            _configCommander = config.SleepWatch?.NetworkCommander;

            _sessionManager = sessionManager;

            _wakeModes = new Dictionary<string, IWakeMode>();
            foreach (IWakeMode mode in modes)
                _wakeModes[mode.Identifier] = mode;

            _wakeNetworks = new Dictionary<string, Network>();

            if (_configCommander != null)
                ConfigureNetworks();

            void ConfigureNetworks()
            {
                if (_configCommander.Host.Count > 0)
                {
                    ConfigureNetworkHosts(_wakeNetworks[null] = new Network(), _configCommander.Host.Values);
                }

                foreach (NetworkInfo networkInfo in _configCommander.Network.Values)
                {
                    Guid? guid = networkInfo.ID != null ? Guid.Parse(networkInfo.ID) : (Guid?)null;

                    ConfigureNetworkHosts(_wakeNetworks[networkInfo.Name] = new Network(networkInfo.Name, guid), networkInfo.Host.Values);
                }
            }
            void ConfigureNetworkHosts(Network network, IEnumerable<HostInfo> hostInfos)
            {
                foreach (HostInfo info in hostInfos)
                {
                    var target = new NetworkTarget(info.Name, info.PhysicalAddress);
                    target.WakeModeChanged += OnWakeModeChanged;
                    network.AddTarget(target);
                }
            }
        }

        [Autowired]
        ILogger<NetworkCommander> Logger { get; set; }

        public IEnumerable<Network> Networks => _wakeNetworks.Values;
        public Network GetNetworkByName(string name) => _wakeNetworks[name];

        public event EventHandler<NetworkEventArgs> NetworkAvailabilityChanged;
        public event EventHandler<NetworkTargetEventArgs> NetworkTargetChanged;

        void IStartable.Start()
        {
            if (_configCommander != null && _configCommander.Wake != WakeFrequency.Never)
            {
                _wakeTimer = new Timer();
                _wakeTimer.AutoReset = true;
                _wakeTimer.Interval = _config.Interval;
                _wakeTimer.Elapsed += OnWakeEvent;

                if (_configCommander.RequiredPresence == WakePresence.User)
                    _sessionManager.UserPresent += OnWakeEvent;

                OnWakeEvent(this, EventArgs.Empty);
            }
        }

        void IPowerEventHandler.OnPowerEvent(PowerBroadcastStatus status)
        {
            switch (status)
            {
                case PowerBroadcastStatus.ResumeSuspend:
                    if (_configCommander.RequiredPresence == WakePresence.System)
                        OnRequiredPresence(this, EventArgs.Empty);
                    break;

                case PowerBroadcastStatus.Suspend:
                    _wakeTimer?.Stop();
                    break;
            }
        }

        private void OnWakeModeChanged(object sender, EventArgs args)
        {
            NetworkTarget target = sender as NetworkTarget;

            Network targetNetwork = null;
            foreach (var network in _wakeNetworks.Values)
                foreach (var networkTarget in network.Targets)
                    if (networkTarget == target)
                        targetNetwork = network;

            if (targetNetwork == null)
                throw new InvalidOperationException("NetworkTarget unknown");

            NetworkTargetChanged?.Invoke(this, new NetworkTargetEventArgs(targetNetwork, target));
        }
        private void OnRequiredPresence(object sender, EventArgs args)
        {
            if (_configCommander.Wake == WakeFrequency.OneTime)
                OnWakeEvent(sender, args);
            else if (_configCommander.Wake == WakeFrequency.Continuous)
                _wakeTimer.Start();
        }
        private void OnWakeEvent(object sender, EventArgs args)
        {
            foreach (Network network in _wakeNetworks.Values)
                WakeNetwork(network);
        }

        private void WakeNetwork(Network network)
        {
            bool wasAvailable = network.IsAvailable;

            network.Connection = Network.NetworkConnection.None;
            foreach (var net in NetworkListManager.GetNetworks(NetworkConnectivityLevels.Connected))
                if (network.Name.Equals(net.Name.ToLower()) || network.Guid == net.NetworkId)
                    network.Connection = Network.NetworkConnection.Ethernet;

            if (wasAvailable != network.IsAvailable)
                NetworkAvailabilityChanged?.Invoke(this, new NetworkEventArgs(network));

            if (network.IsAvailable)
                foreach (NetworkTarget target in network.Targets)
                    WakeTarget(target);
        }
        private void WakeTarget(NetworkTarget target)
        {
            if (!_wakeModes.TryGetValue(target.WakeMode, out IWakeMode mode))
                throw new ArgumentException($"Unknown Wake-Mode: {target.WakeMode}");

            mode.WakeTarget(target);
        }

        void IDisposable.Dispose()
        {
            _sessionManager.UserPresent -= OnWakeEvent;
            _sessionManager.UserLogin -= OnWakeEvent;

            _wakeTimer?.Stop();
            _wakeTimer?.Dispose();
            _wakeTimer = null;
        }

        public interface IWakeMode
        {
            public string Identifier { get; }

            public bool IsTargetSupported(NetworkTarget target);

            public void WakeTarget(NetworkTarget target);
        }

        internal class WakeModeNone : IWakeMode
        {
            public const string ID = "none";

            string IWakeMode.Identifier => ID;

            bool IWakeMode.IsTargetSupported(NetworkTarget target)
            {
                return true;
            }

            void IWakeMode.WakeTarget(NetworkTarget target)
            {
                // do actually nothing
            }
        }
        internal class WakeModeWOL : IWakeMode
        {
            public const string ID = "wakeOnLAN";

            InsomniaConfig _config;
            NetworkCommanderConfig _configCommander;

            public WakeModeWOL(InsomniaConfig config)
            {
                _config = config;
                _configCommander = config.SleepWatch?.NetworkCommander;
            }

            [Autowired]
            ILogger<NetworkCommander> Logger { get; set; }

            string IWakeMode.Identifier => ID;

            bool IWakeMode.IsTargetSupported(NetworkTarget target)
            {
                return target.PhysicalAddress != null;
            }

            void IWakeMode.WakeTarget(NetworkTarget target)
            {
                using (Logger.BeginScope("{target}", target))
                    try
                    {
                        IPAddress ip = IPAddress.Broadcast;

                        if (_configCommander.ResolveIPAddress)
                        {
                            try
                            {
                                ip = Dns.GetHostEntry(target.Name).AddressList.First(addr => addr.AddressFamily == AddressFamily.InterNetwork);
                            }
                            catch (Exception e)
                            {
                                Logger.LogError(e, "IP-Resolution failed");
                            }
                        }

                        if (Logger.IsEnabled(LogLevel.Debug))
                            Logger.LogDebug(InsomniaEventId.WAKE_ON_LAN, $"Sending WOL -> {ip}:{_config.Port} ({target.Name})");

                        SendMagicPacket(target.PhysicalAddress, ip, _config.Port);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e, "Wake failed");
                    }
            }

            private static void SendMagicPacket(PhysicalAddress address, IPAddress ip, int port)
            {
                UdpClient udp = new UdpClient();

                try
                {
                    udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);

                    int offset = 0;
                    byte[] buffer = new byte[6 + 6 * 16];

                    //first 6 bytes should be 0xFF
                    for (int y = 0; y < 6; y++)
                        buffer[offset++] = 0xFF;

                    //now repeate MAC 16 times
                    for (int y = 0; y < 16; y++)
                    {
                        byte[] adrBytes = address.GetAddressBytes();

                        for (int z = 0; z < 6; z++)
                        {
                            buffer[offset++] = adrBytes[z];
                        }
                    }

                    udp.EnableBroadcast = true;
                    udp.Send(buffer, buffer.Length, new IPEndPoint(ip, port));
                }
                finally
                {
                    udp.Close();
                }
            }
        }

        public class Network
        {
            IDictionary<string, NetworkTarget> _targets;

            internal Network(string name = null, Guid? guid = null)
            {
                Name = name;
                Guid = guid;

                _targets = new Dictionary<string, NetworkTarget>();
            }

            public string Name { get; private set; }

            internal Guid? Guid { get; private set; }

            public bool IsAvailable => Connection != NetworkConnection.None;

            public NetworkConnection Connection { get; internal set; } = NetworkConnection.None;

            public IEnumerable<NetworkTarget> Targets => _targets.Values;
            public NetworkTarget GetTargetByName(string name) => _targets[name];

            internal void AddTarget(NetworkTarget target)
            {
                _targets.Add(target.Name, target);
            }
            internal void RemoveTarget(NetworkTarget target)
            {
                _targets.Remove(target.Name);
            }

            public enum NetworkConnection
            {
                None = 0,

                Ethernet,
                Moonrise
            }
        }
        public class NetworkTarget
        {
            string _wakeMode;

            WakeModeEnumerator _wakeModeEnumerator;

            internal NetworkTarget(string name, PhysicalAddress address = null, WakeModeEnumerator enumerator = null)
            {
                Name = name;

                PhysicalAddress = address;

                _wakeModeEnumerator = enumerator ?? DefaultEnumerator;
            }

            public string Name { get; private set; }

            public IEnumerable<string> AvailableWakeModes => _wakeModeEnumerator(this);
            public string WakeMode
            {
                get
                {
                    if (!AvailableWakeModes.Contains(_wakeMode))
                        if (!AvailableWakeModes.Contains(WakeModeWOL.ID))
                            return WakeModeWOL.ID;
                        else
                            return WakeModeNone.ID;

                    return _wakeMode;
                }
                set
                {
                    if (_wakeMode != value)
                    {
                        if (!AvailableWakeModes.Contains(value))
                            throw new ArgumentException(value);

                        _wakeMode = value;

                        WakeModeChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }

            public PhysicalAddress PhysicalAddress { get; private set; }

            public event EventHandler WakeModeChanged;

            internal delegate IEnumerable<string> WakeModeEnumerator(NetworkTarget target);
            private IEnumerable<string> DefaultEnumerator(NetworkTarget target)
            {
                yield return WakeModeNone.ID;

                if (PhysicalAddress != null)
                    yield return WakeModeWOL.ID;
            }
        }

        public class NetworkEventArgs : EventArgs
        {
            internal NetworkEventArgs(Network network)
            {
                Network = network;
            }

            public Network Network { get; private set; }
        }
        public class NetworkTargetEventArgs : NetworkEventArgs
        {
            internal NetworkTargetEventArgs(Network network, NetworkTarget target) : base(network)
            {
                Target = target;
            }

            public NetworkTarget Target { get; private set; }
        }
    }
}