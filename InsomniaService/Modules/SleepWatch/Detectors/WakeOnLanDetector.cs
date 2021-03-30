using MadWizard.Insomnia.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
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
        IDictionary<string, HostInfo> _hostsInfo;

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
            _hostsInfo = new Dictionary<string, HostInfo>();

            _udpClient = new UdpClient();
            _udpClient.EnableBroadcast = true;
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, config.Port));

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
                    UdpReceiveResult result = await _udpClient.ReceiveAsync().ConfigureAwait(false);

                    Payload? payload;
                    try
                    {
                        PhysicalAddress mac = ParseMagicPacket(result.Buffer, out payload);

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

                    lock (_hostsInfo)
                    {
                        if (!_hostsInfo.TryGetValue(host, out HostInfo info))
                            _hostsInfo[host] = info = new HostInfo(host);

                        info.LastSeen = DateTime.Now;

                        if (payload != null)
                        {
                            switch (payload.Value.Type)
                            {
                                case Payload.PayloadType.SERVICE_NAME:
                                    if (payload.Value.Version == 1)
                                    {
                                        string serviceName = payload.Value.Text;
                                        if (!info.Services.TryGetValue(serviceName, out HostInfo.ServiceInfo serviceInfo))
                                            info.Services[serviceName] = serviceInfo = new HostInfo.ServiceInfo(serviceName);

                                        int connectionCount = payload.Value.Header[5];

                                        if (connectionCount > serviceInfo.ConnectionCount)
                                            serviceInfo.ConnectionCount = connectionCount;
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
            catch (ObjectDisposedException) { }
            catch (Exception e)
            {
                File.WriteAllText(@"C:\error.log", e.ToString());

                Logger.LogError(e, "Listen UDP-Socket failed.");
            }
        }

        (string[] tokens, bool busy) ActivityDetector.IDetector.Scan()
        {
            List<string> tokenList = new List<string>();

            lock (_hostsInfo)
            {
                foreach (HostInfo info in _hostsInfo.Values)
                {
                    TimeSpan time = DateTime.Now - info.LastSeen;

                    if (time.TotalMilliseconds < _config.Interval)
                    {
                        string token = info.Name;
                        if (info.Services.Count > 0)
                        {
                            List<string> serviceInfos = new List<string>();
                            foreach (var serviceInfo in info.Services.Values)
                            {
                                string serviceDesc = "";
                                serviceDesc += serviceInfo.Name;
                                if (serviceInfo.ConnectionCount > 0)
                                    serviceDesc += "#" + serviceInfo.ConnectionCount ;

                                serviceInfos.Add(serviceDesc);
                            }
                        
                            token += "[" + string.Join(", ", serviceInfos) + "]";
                        }

                        tokenList.Add(token);
                    }

                    info.Services.Clear();
                }
            }

            return (tokenList.ToArray(), tokenList.Count > 0);
        }

        void IDisposable.Dispose()
        {
            _udpClient?.Close();
            _udpClient = null;
        }

        private static PhysicalAddress ParseMagicPacket(byte[] buffer, out Payload? payload)
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

            if (buffer.Length > offset)
            {
                if (!(buffer.Length > offset + 6))
                    throw new MagicPacketNotValid("TOO_SHORT");

                byte[] payloadData = new byte[buffer.Length - offset];
                Array.Copy(buffer, offset, payloadData, 0, payloadData.Length);
                payload = new Payload(payloadData);
            }
            else
                payload = null;

            return mac;
        }

        private class HostInfo
        {
            public string Name { get; private set; }
            public DateTime LastSeen { get; set; }
            public IDictionary<string, ServiceInfo> Services { get; private set; } = new Dictionary<string, ServiceInfo>();

            internal HostInfo(string name)
            {
                Name = name;
            }

            internal class ServiceInfo
            {
                public string Name { get; private set; }
                public int ConnectionCount { get; set; }

                internal ServiceInfo(string name)
                {
                    Name = name;
                }
            }
        }

        private struct Payload
        {
            // HEADER: TYPE, VERSION, X, X, X, X

            internal enum PayloadType
            {
                SERVICE_NAME = 1,
            }

            private byte[] header;
            private byte[] data;

            internal Payload(byte[] payload)
            {
                header = new byte[6];
                data = new byte[payload.Length - header.Length];

                Array.Copy(payload, 0, header, 0, header.Length);
                Array.Copy(payload, header.Length, data, 0, data.Length);
            }

            internal Payload(PayloadType type, string data, byte version = 1)
            {
                this.header = new byte[] { (byte)(int)type, version, 0, 0, 0, 0 };
                this.data = Encoding.UTF8.GetBytes(data);
            }

            public byte[] Header => this.header;
            public PayloadType Type => (PayloadType)this.header[0];
            public byte Version => this.header[1];

            public byte[] Data => this.data;
            public string Text => Encoding.UTF8.GetString(data);
        }

        private class MagicPacketNotValid : ArgumentException
        {
            internal MagicPacketNotValid(string code) : base($"Not a valid Magic-Packet. [{code}]") { }
        }
    }

}