using Autofac;
using Autofac.Core;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Lifetime;
using MadWizard.Insomnia.Service.Sessions;
using MadWizard.Insomnia.Service.SleepWatch;
using MadWizard.Insomnia.Tools;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Targets;
using PacketDotNet;
using SharpPcap;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static MadWizard.Insomnia.Configuration.SleepWatchConfig.ActivityDetectorConfig;
using static MadWizard.Insomnia.Configuration.SleepWatchConfig.ActivityDetectorConfig.NetworkActivityConfig;

namespace MadWizard.Insomnia.Service.SleepWatch.Detector
{
    class NetworkActivity : ActivityDetector.IDetector, IPowerEventHandler, IStartable, IDisposable
    {
        NetworkActivityConfig _config;

        IDictionary<string, NetworkHost> _hosts;

        ILiveDevice _device;

        public NetworkActivity(InsomniaConfig config)
        {
            _config = config.SleepWatch.ActivityDetector.NetworkActivity;

            _hosts = new Dictionary<string, NetworkHost>();

            foreach (var hostInfo in _config.Host)
                _hosts.Add(hostInfo.Key, new NetworkHost(hostInfo.Value));
            foreach (var hostInfo in _config.HyperVHost)
                _hosts.Add(hostInfo.Key, new NetworkHost(hostInfo.Value));

        }

        [Autowired]
        ILogger<NetworkActivity> Logger { get; set; }

        void IStartable.Start()
        {
            foreach (var device in CaptureDeviceList.Instance)
            {
                if (device.Name.Contains(_config.Interface))
                {
                    _device = device;

                    device.Open();
                    device.OnPacketArrival += Device_OnPacketArrival;
                    device.StartCapture();

                    Logger.LogInformation(InsomniaEventId.MONITOR_NETWORK, $"Monitoring network interface: {device.Description}");

                    break;
                }
            }
        }

        void IDisposable.Dispose()
        {
            if (_device != null)
            {
                _device.StopCapture();
                _device.Dispose();
                _device = null;
            }
        }

        private void Device_OnPacketArrival(object s, PacketCapture e)
        {
            var raw = e.GetPacket();

            var packet = Packet.ParsePacket(raw.LinkLayerType, raw.Data);

            NetworkHost host = null;
            Service service = null;
            if (packet is EthernetPacket ethernet)
            {
                if (ethernet.Type == EthernetType.Arp && ethernet.PayloadPacket is ArpPacket arp)
                {
                    if (arp.Operation != ArpOperation.Request)
                        return;
                    if (arp.SenderProtocolAddress.Equals(arp.TargetProtocolAddress))
                        return;

                    if (MatchesHost(arp.TargetProtocolAddress, out host) && host.HyperV != null)
                    {
                        if (host.HyperV.State != VirtualMachineState.Running)
                        {
                            var targetMAC = arp.SenderHardwareAddress;
                            var sourceMAC = host.HostMAC;
                            var targetIP = arp.SenderProtocolAddress;
                            var sourceIP = arp.TargetProtocolAddress;

                            var response = new EthernetPacket(sourceMAC, targetMAC, EthernetType.Arp)
                            {
                                PayloadPacket = new ArpPacket(ArpOperation.Response, targetMAC, targetIP, sourceMAC, sourceIP)
                            };

                            _device?.SendPacket(response);

                            Logger.LogInformation($"Send ARP Response for {host.Name}");
                        }
                    }
                }

                // IPv4
                if (ethernet.Type == EthernetType.IPv4 && ethernet.PayloadPacket is IPv4Packet ip4)
                {
                    if (MatchesHost(ip4.DestinationAddress, out host))
                    {
                        if (host.MatchesService(ip4.PayloadPacket, out service))
                        {
                            service.AccessCount++;
                        }
                    }
                }

                // IPv6
                if (ethernet.Type == EthernetType.IPv6 && ethernet.PayloadPacket is IPv6Packet ip6)
                {
                    if (MatchesHost(ip6.DestinationAddress, out host))
                    {
                        if (host.MatchesService(ip6.PayloadPacket, out service))
                        {
                            service.AccessCount++;
                        }
                    }
                }
            }

            if (host?.HyperV != null && service != null)
            {
                if (host.HyperV.State != VirtualMachineState.Running && host.HyperV.AutoStart)
                {
                    host.HyperV.Start();
                }
            }
        }

        private bool MatchesHost(PhysicalAddress mac, out NetworkHost foundHost)
        {
            foundHost = null;

            foreach (NetworkHost host in _hosts.Values)
            {
                if (mac.Equals(host.HostMAC))
                { 
                    foundHost = host; 
                    break; 
                }
            }

            return false;
        }

        private bool MatchesHost(IPAddress ip, out NetworkHost foundHost)
        {
            foundHost = null; 

            foreach (NetworkHost host in _hosts.Values)
            {
                foreach (IPAddress hostIP in host.HostIPs)
                    if (ip.Equals(hostIP))
                    {
                        foundHost = host;
                        return true;
                    }
            }

            return false;
        }

        (string[] tokens, bool busy) ActivityDetector.IDetector.Scan()
        {
            var tokenList = new HashSet<string>();

            lock (_hosts)
            {
                foreach (var host in _hosts.Values)
                    foreach (var service in host.Services.Values)
                    {
                        if (service.AccessCount > service.AccessThreshold)
                        {
                            tokenList.Add($"*{host.Name}:{service.Name}*");
                        }

                        service.AccessCount = 0;
                    }
            }

            return (tokenList.ToArray(), tokenList.Count > 0);
        }

        private void SendARPAnnouncement()
        {
            foreach (var host in _hosts.Values)
            {
                if (host.HyperV is not null)
                {
                    if (host.HyperV.State != VirtualMachineState.Running)
                    {
                        var targetMAC = PhysicalAddress.Parse("00:00:00:00:00:00");
                        var sourceMAC = host.HostMAC;
                        var targetIP = host.HostIPv4Address;
                        var sourceIP = host.HostIPv4Address;

                        var response = new EthernetPacket(sourceMAC, PhysicalAddress.Parse("FF:FF:FF:FF:FF:FF"), EthernetType.Arp)
                        {
                            PayloadPacket = new ArpPacket(ArpOperation.Request, targetMAC, targetIP, sourceMAC, sourceIP)
                        };

                        _device?.SendPacket(response);

                        Logger.LogInformation($"Send ARP Announcement for {host.Name}");

                    }
                }
            }
        }

        async void IPowerEventHandler.OnPowerEvent(PowerBroadcastStatus status)
        {
            var retries = 5;

            switch (status)
            {
                case PowerBroadcastStatus.Suspend:
                    break;

                case PowerBroadcastStatus.ResumeSuspend:
                    while (retries-- > 0)
                        try
                        {
                            await Task.Delay(1000);

                            SendARPAnnouncement();

                            break;
                        }
                        catch (Exception ex)
                        {
                            continue;
                        }
                    break;
            }

        }
    }

    internal class NetworkHost
    {
        internal string Name { get; private set; }

        internal string HostName { get; private set; }
        internal ISet<IPAddress> HostIPs { get; private set; }
        internal PhysicalAddress HostMAC { get; private set; }
        internal IPAddress HostIPv4Address { get; private set; }

        internal IDictionary<string, Service> Services { get; private set; }

        internal HyperVData HyperV { get; private set; }

        internal NetworkHost(NetworkHostInfo info)
        {
            Name = info.Name;

            HostName = Name;

            Services = new Dictionary<string, Service>();

            foreach (var serviceInfo in info.TCPService)
            {
                Services.Add(serviceInfo.Key, new TCPService(serviceInfo.Value));
            }

            if (info is HyperVHostInfo hyper)
                HyperV = new HyperVData(hyper);

            this.ResolveAddresses(info);
        }

        internal void ResolveAddresses(NetworkHostInfo info)
        {
            HostIPs = new HashSet<IPAddress>(Dns.GetHostEntry(HostName).AddressList);

            if (info.IPv4Address != null)
                HostIPs.Add(info.IPv4Address);
            if (info.PhysicalAddress != null)
                HostMAC = info.PhysicalAddress;
            if (info.IPv4Address != null)
                HostIPv4Address = info.IPv4Address;
        }

        public bool MatchesService(Packet packet, out Service foundService)
        {
            foundService = null;

            foreach (Service service in Services.Values)
            {
                if (service is TCPService tcpService && packet is TcpPacket tcp)
                {
                    if (tcp.DestinationPort == tcpService.Port)
                    {
                        foundService = tcpService;
                        return true;
                    }
                }
            }

            return false;
        }
    }

    internal class HyperVData
    {
        internal string Name { get; set; }
        internal bool AutoStart { get; init; }
        internal bool AutoSuspend { get; init; }

        internal HyperVData(HyperVHostInfo info)
        {
            Name = info.Name;

            AutoStart = info.OnAccess == RequestedState.Enabled;
            AutoSuspend = info.OnIdle == RequestedState.Offline;
        }

        internal VirtualMachineState State => HyperVAPI.GetVirtualMachineState(Name);

        internal void Start()
        {
            Task.Run(() => HyperVAPI.StartVirtualMachine(Name));
        }

        internal void Suspend()
        {
            Task.Run(() => HyperVAPI.SuspendVirtualMachine(Name));
        }
    }

    internal abstract class Service
    {
        internal string Name { get; private set; }

        internal int AccessCount { get; set; } = 0;
        internal int AccessThreshold { get; set; } = 1;

        internal Service(string name)
        {
            Name = name;
        }
    }

    internal class TCPService : Service
    {
        internal int Port { get; private set; }

        internal TCPService(TCPServiceInfo info) : base(info.Name)
        {
            Port = info.Port;

            AccessThreshold = info.Threshold;
        }
    }
}

