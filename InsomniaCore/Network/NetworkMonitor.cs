using Autofac;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Network.Host;
using MadWizard.Insomnia.Network.Services;
using MadWizard.Insomnia.Power.Manager;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using System.Net.NetworkInformation;

namespace MadWizard.Insomnia.Network
{
    public class NetworkMonitor(NetworkMonitorConfig config, NetworkSniffer sniffer) 
        : NetworkHostMonitor(config), IStartable
    {
        public required ILogger<NetworkMonitor> Logger { get; set; }

        public required IPowerManager PowerManager { get; set; }

        void IStartable.Start()
        {
            if (sniffer.IsAvailable)
            {
                base.ConfigureHosts();

                sniffer.PacketReceived += Sniffer_PacketReceived;
                PowerManager.ResumeSuspended += PowerManager_ResumeSuspend;

                Logger.LogDebug("Startup complete");
            }
            else
            {
                Logger.LogWarning("Startup failed");

            }
        }

        private void Sniffer_PacketReceived(object? s, Packet packet)
        {
            if (packet is EthernetPacket ethernet)
            {
                // Handle ARP Requests
                if (ethernet.Type == EthernetType.Arp && ethernet.PayloadPacket is ArpPacket arp)
                {
                    if (arp.Operation != ArpOperation.Request)
                        return;
                    if (arp.SenderProtocolAddress.Equals(arp.TargetProtocolAddress))
                        return;

                    if (FindHost(arp.TargetProtocolAddress, out NetworkHost? host) && host is VirtualHost vm)
                    {
                        if (vm.VHost.State != VirtualHostState.Running)
                        {
                            var targetMAC = arp.SenderHardwareAddress;
                            var sourceMAC = host.HostMAC;
                            var targetIP = arp.SenderProtocolAddress;
                            var sourceIP = arp.TargetProtocolAddress;

                            var response = new EthernetPacket(sourceMAC, targetMAC, EthernetType.Arp)
                            {
                                PayloadPacket = new ArpPacket(ArpOperation.Response, targetMAC, targetIP, sourceMAC, sourceIP)
                            };

                            sniffer.SendPacket(response);

                            Logger.LogInformation($"Send ARP response on behalf of '{host.Name}'");
                        }
                    }
                }

                // Handle IP Service Requests
                if ((ethernet.Type == EthernetType.IPv4 || ethernet.Type == EthernetType.IPv6) && ethernet.PayloadPacket is IPPacket ip)
                    if (FindHost(ip.DestinationAddress, out NetworkHost? host) && host is not null)
                        if (host.MatchService(ip.PayloadPacket, out NetworkService? service))
                            service?.AccessBy(ip.PayloadPacket);
            }
        }

        private async void PowerManager_ResumeSuspend(object? sender, EventArgs e)
        {
            Logger.LogDebug($"Trying to send ARP announcements for virtual hosts.");

            const int retryCount = 8;
            int retry = 0;

            while (true)
                try
                {
                    foreach (var host in this) if (host is VirtualHost vm)
                        if (vm.VHost.State != VirtualHostState.Running)
                            SendARPAnnouncement(host);

                    break;
                }
                catch (SharpPcap.PcapException ex)
                {
                    if (++retry > retryCount)
                    {
                        Logger.LogError(ex, $"Could not send ARP Announcement after {retry} retries.");

                        break;
                    }

                    await Task.Delay(500);
                }
        }

        private void SendARPAnnouncement(NetworkHost host)
        {
            var response = new EthernetPacket(host.HostMAC, PhysicalAddressExt.Broadcast, EthernetType.Arp)
            {
                PayloadPacket = new ArpPacket(ArpOperation.Request, 
                    targetHardwareAddress: PhysicalAddressExt.Empty, 
                    targetProtocolAddress: host.HostIPv4Address, 
                    senderHardwareAddress: host.HostMAC, 
                    senderProtocolAddress: host.HostIPv4Address)
            };

            sniffer.SendPacket(response);

            Logger.LogInformation($"Send ARP Announcement for {host.Name}");
        }
    }
}