using Autofac;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PacketDotNet;
using SharpPcap.LibPcap;
using SharpPcap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MadWizard.Insomnia.Configuration;

namespace MadWizard.Insomnia.Network
{
    public class NetworkSniffer(INetworkInterfaceConfig config) : IStartable, IDisposable
    {
        public required ILogger<NetworkSniffer> Logger { private get; init; }

        private readonly IList<Packet> _sentPackets = [];

        public bool IsAvailable => Device != null;
        private bool IsMaxResponsiveness { get; set; }
        private bool IsNoCaptureLocal { get; set; }

        private ILiveDevice? Device { get; set; }

        public PhysicalAddress? PhysicalAddress => Device?.MacAddress;
        public IPAddress? IPv4Address
        {
            get
            {
                if (Device is LibPcapLiveDevice pcap)
                {
                    return pcap.Addresses.Where(address => address.Addr.ipAddress?.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).SingleOrDefault()?.Addr.ipAddress;
                }

                return null;
            }
        }

        public event EventHandler<Packet>? PacketReceived;

        void IStartable.Start()
        {
            using var scope = this.Logger?.BeginScope(config.Interface);

            if (config.Interface != null)
            try
            {
                foreach (var device in CaptureDeviceList.Instance)
                {
                    if (CheckDeviceName(device, config.Interface))
                    {
                        if (TryOpen(device, out bool maxResponsiveness, out bool noCaptureLocal))
                        {
                            Device = device;

                            Device.OnPacketArrival += Device_OnPacketArrival;
                            Device.StartCapture();

                            List<string> features = [];
                            if (IsMaxResponsiveness = maxResponsiveness)
                                features.Add("MaxResponsiveness");
                            if (IsNoCaptureLocal = noCaptureLocal)
                                features.Add("NoCaptureLocal");

                            Logger?.LogInformation($"Monitoring network interface \"{device.Description ?? device.Name}\", MAC={PhysicalAddress?.ToHexString()}, IPv4={IPv4Address?.ToString()} [{string.Join(", ", features)}]");

                            break;
                        }
                        else
                        {
                            Logger?.LogError($"Failed to start open network interface \"{device.Description ?? device.Name}\"");
                        }
                    }
                }

                if (Device == null)
                {
                    Logger?.LogWarning($"Network interface \"{config.Interface}\" not found");
                }
            }
            catch (DllNotFoundException ex)
            {
                Logger?.LogError(ex, "NetworkSniffer could not be started.");
            }
        }

        private bool CheckDeviceName(ILiveDevice device, string name)
        {
            if (device is PcapDevice pcap)
            {
                if (pcap.Interface.Name.Contains(name))
                    return true;
                if (pcap.Interface.Description.Contains(name))
                    return true;
                if (pcap.Interface?.FriendlyName?.Contains(name) ?? false)
                    return true;
            }
            else
            {
                if (device.Name.Contains(name))
                    return true;
            }

            return false;
        }

        private bool TryOpen(ILiveDevice device, out bool maxResponsiveness, out bool noCaptureLocal)
        {
            try
            {
                device.Open(DeviceModes.Promiscuous | DeviceModes.MaxResponsiveness | DeviceModes.NoCaptureLocal);

                maxResponsiveness = true;
                noCaptureLocal = true;

                return true;
            }
            catch (PcapException)
            {

            }

            try
            {
                device.Open(DeviceModes.Promiscuous | DeviceModes.MaxResponsiveness);

                maxResponsiveness = true;
                noCaptureLocal = false;

                return true;
            }
            catch (PcapException)
            {

            }

            try
            {
                device.Open(DeviceModes.Promiscuous);

                maxResponsiveness = false;
                noCaptureLocal = false;

                return true;
            }
            catch (PcapException)
            {
                maxResponsiveness = false;
                noCaptureLocal = false;

                return false;
            }
        }

        private void Device_OnPacketArrival(object sender, PacketCapture capture)
        {
            if (!FilterInjectedPacket(capture))
            {
                var raw = capture.GetPacket();
                var packet = Packet.ParsePacket(raw.LinkLayerType, raw.Data);

                PacketReceived?.Invoke(this, packet);
            }
        }

        private bool FilterInjectedPacket(PacketCapture capture)
        {
            lock (_sentPackets)
                foreach (var sent in _sentPackets)
                    if (capture.Data.SequenceEqual(sent.Bytes))
                        return _sentPackets.Remove(sent);

            return false;
        }

        public void SendPacket(Packet packet)
        {
            Device.SendPacket(packet);

            if (!IsNoCaptureLocal)
                lock (_sentPackets)
                    _sentPackets.Add(packet);
        }

        void IDisposable.Dispose()
        {
            if (Device != null)
            {
                Device.StopCapture();
                Device.Close();
                Device.Dispose();
                Device = null;
            }
        }
    }
}
