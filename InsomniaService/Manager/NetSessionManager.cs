using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;

namespace MadWizard.Insomnia.NetworkSession.Manager
{
    public class NetSessionManager() : INetworkSessionManager
    {
        public required ILogger<NetSessionManager> Logger { private get; init; }

        public IEnumerator<INetworkSession> GetEnumerator()
        {
            nint pResumeHandle = 0;

            var status = NetSessionEnum(
                null, // local computer
                null, // client name
                null, // username
                502, // include all info
                out nint pSessionInfo, // pointer to SESSION_INFO_502[]
                prefMaxLen: -1,
                out uint entriesRead,
                out uint totalEntries,
                ref pResumeHandle
            );

            try
            {
                if (status != NET_API_STATUS.NERR_Success)
                    throw new InvalidOperationException(status.ToString());
                if (entriesRead < totalEntries)
                    Logger.LogWarning($"EnumerateSessions() incomplete ({entriesRead} / {totalEntries}");

                Console.WriteLine("Read {0} of {1} entries", entriesRead, totalEntries);

                for (int i = 0; i < entriesRead; i++)
                {
                    nint next = new(pSessionInfo.ToInt64() + (SESSION_INFO_502.SIZE * i));

                    var data = (SESSION_INFO_502)Marshal.PtrToStructure(next, typeof(SESSION_INFO_502))!;

                    yield return new NetSessionInfo(data)
                    {
                        IPAddress = DetermineIPAddress(data.cname),
                        ComputerName = DetermineComputerName(data.cname)
                    };
                }
            }
            finally
            {
                NetApiBufferFree(pSessionInfo);
            }
        }

        private static string? DetermineComputerName(string name)
        {
            try
            {
                var ip = IPAddress.Parse(name);

                try
                {
                    IPHostEntry entry = Dns.GetHostEntry(ip);

                    var parts = entry.HostName.Split("."); // PARALLELWELT[.fritz.box]

                    return parts[0];
                }
                catch (SocketException)
                {
                    return null;
                }
            }
            catch (FormatException)
            {
                return name;
            }
        }

        private static IPAddress? DetermineIPAddress(string name)
        {
            try
            {
                return IPAddress.Parse(name);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private class NetSessionInfo(SESSION_INFO_502 info) : INetworkSession
        {
            public string UserName => info.username;

            public string? ComputerName { get; init; }
            public IPAddress? IPAddress { get; init; }

            public int? NumberOfOpenFiles => (int)info.num_opens;

            public TimeSpan ConnectionTime => new(0, 0, (int)info.time);
            public TimeSpan IdleTime => new(0, 0, (int)info.idle_time);

            public bool IsGuest => info.user_flags == SESSION_INFO_502_USER_FLAGS.SESS_GUEST;
        }

        #region API: Network-Sessions
        [DllImport("netapi32.dll", SetLastError = true)]
        private static extern NET_API_STATUS NetSessionEnum(
            string? serverName,
            string? uncClientName,
            string? userName,
            UInt32 level,
            out IntPtr bufPtr,
            int prefMaxLen,
            out UInt32 entriesRead,
            out UInt32 totalEntries,
            ref IntPtr resume_handle
        );

        [DllImport("netapi32.dll")]
        private static extern uint NetApiBufferFree(IntPtr Buffer);

        private enum NET_API_STATUS : uint
        {
            NERR_Success = 0,
            NERR_InvalidComputer = 2351,
            NERR_NotPrimary = 2226,
            NERR_SpeGroupOp = 2234,
            NERR_LastAdmin = 2452,
            NERR_BadPassword = 2203,
            NERR_PasswordTooShort = 2245,
            NERR_UserNotFound = 2221,
            ERROR_ACCESS_DENIED = 5,
            ERROR_NOT_ENOUGH_MEMORY = 8,
            ERROR_INVALID_PARAMETER = 87,
            ERROR_INVALID_NAME = 123,
            ERROR_INVALID_LEVEL = 124,
            ERROR_MORE_DATA = 234,
            ERROR_SESSION_CREDENTIAL_CONFLICT = 1219
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SESSION_INFO_502
        {
            public static readonly int SIZE = Marshal.SizeOf(typeof(SESSION_INFO_502));

            public string cname;
            public string username;
            public uint num_opens;
            public uint time;
            public uint idle_time;
            public SESSION_INFO_502_USER_FLAGS user_flags;
            public string cltype_name;
            public string transport;
        }

        private enum SESSION_INFO_502_USER_FLAGS : uint
        {
            SESS_GUEST = 1,
            SESS_NOENCRYPTION = 2
        }
        #endregion
    }


}
