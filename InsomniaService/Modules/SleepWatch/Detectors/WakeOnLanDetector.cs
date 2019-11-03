using MadWizard.Insomnia.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service.SleepWatch.Detector
{
    class WakeOnLANAnalyzer : ActivityDetector.IDetector, IDisposable
    {
        InsomniaConfig _config;

        ISet<PhysicalAddress> _ownMACs;
        IDictionary<string, DateTime> _hostsLastSeen;

        UdpClient _udpClient;

        public WakeOnLANAnalyzer(InsomniaConfig config)
        {
            static ISet<PhysicalAddress> DetermineMACs()
            {
                ISet<PhysicalAddress> foundMACs = new HashSet<PhysicalAddress>();
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                    if (nic.OperationalStatus == OperationalStatus.Up)
                        foundMACs.Add(nic.GetPhysicalAddress());
                return foundMACs;
            }

            _config = config;

            _ownMACs = DetermineMACs();
            _hostsLastSeen = new Dictionary<string, DateTime>();

            _udpClient = new UdpClient(config.Port);

            Task.Run(() => Listen());

        }

        [Autowired]
        ILogger<WakeOnLANAnalyzer> Logger { get; set; }

        private async void Listen()
        {
            try
            {
                while (true)
                {
                    UdpReceiveResult result = await _udpClient.ReceiveAsync();

                    try
                    {
                        PhysicalAddress mac = ParseMagicPacket(result.Buffer);

                        if (!_ownMACs.Contains(mac))
                            continue;
                    }
                    catch (MagicPacketNotValid)
                    {
                        continue;
                    }

                    IPAddress ip = result.RemoteEndPoint.Address;

                    string host;
                    try
                    {
                        IPHostEntry hostEntry = Dns.GetHostEntry(ip);

                        host = hostEntry.HostName.Split('.')[0];
                    }
                    catch
                    {
                        host = ip.ToString();
                    }

                    if (Logger.IsEnabled(LogLevel.Debug))
                        Logger.LogDebug(InsomniaEventId.WAKE_ON_LAN, $"Received WOL <- {host}");

                    lock (_hostsLastSeen)
                    {
                        _hostsLastSeen[host] = DateTime.Now;
                    }
                }
            }
            catch (ObjectDisposedException) { }
            catch (Exception e)
            {
                Logger.LogError(e, "Listen UDP-Socket failed.");
            }
        }

        (string[] tokens, bool busy) ActivityDetector.IDetector.Scan()
        {
            List<string> tokenList = new List<string>();

            lock (_hostsLastSeen)
            {
                foreach (string host in _hostsLastSeen.Keys)
                {
                    TimeSpan time = DateTime.Now - _hostsLastSeen[host];

                    if (time.TotalMilliseconds < _config.Interval)
                    {
                        tokenList.Add(host);
                    }
                }
            }

            return (tokenList.ToArray(), tokenList.Count > 0);
        }

        void IDisposable.Dispose()
        {
            _udpClient?.Close();
            _udpClient = null;
        }

        private static PhysicalAddress ParseMagicPacket(byte[] buffer)
        {
            int offset = 0;

            if (buffer.Length < (6 + 6 * 16))
                throw new MagicPacketNotValid("TOO_SHORT");

            //first 6 bytes should be 0xFF
            for (int y = 0; y < 6; y++)
                if (buffer[offset++] != 0xFF)
                    throw new MagicPacketNotValid("FIRST_BYTES_NOT_ZERO");

            //now repeate MAC 16 times
            PhysicalAddress mac = null;
            for (int y = 0; y < 16; y++)
            {
                byte[] adrBytes = new byte[6];

                for (int z = 0; z < 6; z++)
                {
                    adrBytes[z] = buffer[offset++];
                }

                if (mac == null)
                    mac = new PhysicalAddress(adrBytes);
                else if (!mac.Equals(new PhysicalAddress(adrBytes)))
                    throw new MagicPacketNotValid("MAC_ADR_NOT_EQUAL");
            }

            if (mac == null)
                throw new MagicPacketNotValid("MAC_ADR_NOT_RECEIVED");

            return mac;
        }

        private class MagicPacketNotValid : ArgumentException
        {
            internal MagicPacketNotValid(string code) : base($"Not a valid Magic-Packet. [{code}]") { }
        }
    }
}