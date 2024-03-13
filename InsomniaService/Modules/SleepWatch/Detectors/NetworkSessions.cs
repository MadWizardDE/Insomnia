using Autofac;
using Autofac.Core;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Tools;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service.SleepWatch.Detector
{
    public class NetworkSessions : ActivityDetector.IDetector
    {
        private InsomniaConfig _config;
        private SleepWatchConfig.ActivityDetectorConfig.NetworkSessionsConfig _configSessions;

        public NetworkSessions(InsomniaConfig config)
        {
            _config = config;
            _configSessions = config.SleepWatch.ActivityDetector.NetworkSessions;
        }

        [Autowired]
        ILogger<NetworkSessions> Logger { get; set; }

        public IList<NetworkSessionInfo> EnumerateSessions()
        {
            IntPtr pSessionInfo;
            IntPtr pResumeHandle = IntPtr.Zero;
            UInt32 entriesRead, totalEntries;

            var netStatus = Win32API.NetSessionEnum(
                null, // local computer
                null, // client name
                null, // username
                502, // include all info
                out pSessionInfo, // pointer to SESSION_INFO_502[]
                Win32API.MAX_PREFERRED_LENGTH,
                out entriesRead,
                out totalEntries,
                ref pResumeHandle
            );

            try
            {
                if (netStatus != Win32API.NET_API_STATUS.NERR_Success)
                    throw new InvalidOperationException(netStatus.ToString());
                if (entriesRead < totalEntries)
                    Logger.LogWarning($"EnumerateSessions() incomplete ({entriesRead} / {totalEntries}");

                Console.WriteLine("Read {0} of {1} entries", entriesRead, totalEntries);

                var list = new List<NetworkSessionInfo>();

                for (int i = 0; i < entriesRead; i++)
                {
                    var data = (Win32API.SESSION_INFO_502) Marshal.PtrToStructure(new IntPtr(pSessionInfo.ToInt64() + (Win32API.SESSION_INFO_502.SIZE * i)), typeof(Win32API.SESSION_INFO_502));

                    list.Add(new NetworkSessionInfo(data));
                }

                return list;
            }
            finally
            {
                Win32API.NetApiBufferFree(pSessionInfo);
            }
        }

        (string[] tokens, bool busy) ActivityDetector.IDetector.Scan()
        {
            var tokenList = new HashSet<string>();

            string localhostName = Dns.GetHostName();
            foreach (var session in EnumerateSessions())
            {
                if (session.ComputerName == localhostName)
                    continue;
                if (session.IdleTime.TotalSeconds > this._config.Interval)
                    continue;
                if (session.NumberOfOpenFiles == 0)
                    continue;

                tokenList.Add($"\\\\{session.ComputerName}\\{session.UserName}[{session.NumberOfOpenFiles}]");
            }

            return (tokenList.ToArray(), tokenList.Count > 0);
        }
    }

    public class NetworkSessionInfo
    {
        private Win32API.SESSION_INFO_502 _sessionInfo;

        private string _resolvedComputerName;

        internal NetworkSessionInfo(Win32API.SESSION_INFO_502 info)
        {
            this._sessionInfo = info;
            this._resolvedComputerName = this.ResolveComputerName();
        }

        private string ResolveComputerName()
        {
            var ip = IPAddress.Parse(this._sessionInfo.cname);

            try
            {
                IPHostEntry entry = Dns.GetHostEntry(ip);

                var parts = entry.HostName.Split("."); // PARALLELWELT[.fritz.box]

                return parts[0];
            }
            catch (SocketException)
            {

            }

            return null;
        }

        public string UserName { get { return this._sessionInfo.username; } }
        public string ComputerName { get { return _resolvedComputerName ?? this._sessionInfo.cname; } }

        public uint NumberOfOpenFiles { get { return this._sessionInfo.num_opens; } }

        public TimeSpan Time { get { return new TimeSpan(0, 0, (int)this._sessionInfo.time); } }
        public TimeSpan IdleTime { get { return new TimeSpan(0, 0, (int)this._sessionInfo.idle_time); } }

        public bool IsGuest { get { return this._sessionInfo.user_flags == Win32API.SESSION_INFO_502_USER_FLAGS.SESS_GUEST; } }

    }
}
