using MadWizard.Insomnia.Configuration;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

namespace MadWizard.Insomnia.Service.SleepWatch.Detector
{
    class PingHostDetector : ActivityDetector.IDetector
    {
        string[] _hosts;

        public PingHostDetector(InsomniaConfig config)
        {
            var scanConfig = config.SleepWatch?.ActivityDetector?.PingHost;

            if (scanConfig != null)
            {
                _hosts = scanConfig.Host.Values.Select(h => h.Name).ToArray();
            }
        }

        (string[] tokens, bool busy) ActivityDetector.IDetector.Scan()
        {
            List<string> tokenList = new List<string>();

            using (Ping ping = new Ping())
                foreach (string host in _hosts)
                {
                    try
                    {
                        PingReply reply = ping.Send(host);
                        if (reply.Status == IPStatus.Success)
                        {
                            tokenList.Add(host);
                        }
                    }
                    catch (PingException)
                    {
                        // loggen?
                    }
                }

            return (tokenList.ToArray(), tokenList.Count > 0);
        }
    }
}
