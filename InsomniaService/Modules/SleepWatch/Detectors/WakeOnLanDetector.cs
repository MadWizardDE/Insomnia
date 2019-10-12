using MadWizard.Insomnia.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service.SleepWatch.Detector
{
    class WakeOnLANAnalyzer : ActivityDetector.IDetector, IDisposable
    {
        InsomniaConfig _config;

        IDictionary<string, DateTime> _hostsLastSeen;

        UdpClient _udpClient;

        public WakeOnLANAnalyzer(InsomniaConfig config)
        {
            _config = config;

            _hostsLastSeen = new Dictionary<string, DateTime>();

            if (config.SleepWatch?.ActivityDetector?.WakeOnLAN != null)
            {
                _udpClient = new UdpClient(config.Port);

                Task.Run(() => Listen());
            }
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
    }
}