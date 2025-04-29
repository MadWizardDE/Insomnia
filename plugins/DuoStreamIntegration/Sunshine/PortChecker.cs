using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service.Duo.Sunshine
{
    internal class PortChecker
    {
        private const int AF_INET = 2; // IPv4

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedUdpTable(nint pUdpTable, ref int pdwSize, bool bOrder, int ulAf, UDP_TABLE_CLASS TableClass, int Reserved);
        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int GetExtendedTcpTable(IntPtr pTcpTable, ref int pdwSize, bool bOrder, int ulAf, TCP_TABLE_CLASS TableClass, int Reserved);

        private enum UDP_TABLE_CLASS
        {
            OWNER_PID = 1
        }

        private enum TCP_TABLE_CLASS
        {
            TCP_TABLE_BASIC_LISTENER,
            TCP_TABLE_BASIC_CONNECTIONS,
            TCP_TABLE_BASIC_ALL,
            TCP_TABLE_OWNER_PID_LISTENER,
            TCP_TABLE_OWNER_PID_CONNECTIONS,
            TCP_TABLE_OWNER_PID_ALL,
            TCP_TABLE_OWNER_MODULE_LISTENER,
            TCP_TABLE_OWNER_MODULE_CONNECTIONS,
            TCP_TABLE_OWNER_MODULE_ALL
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPTABLE_OWNER_PID
        {
            public uint dwNumEntries;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPROW_OWNER_PID
        {
            public uint dwState;
            public uint dwLocalAddr;
            public uint dwLocalPort;
            public uint dwRemoteAddr;
            public uint dwRemotePort;
            public uint dwOwningPid;
        }

        private class TcpConnectionEntry
        {
            public string LocalAddress { get; }
            public int LocalPort { get; }
            public string RemoteAddress { get; }
            public int RemotePort { get; }
            public int ProcessId { get; }
            public string State { get; }

            public TcpConnectionEntry(MIB_TCPROW_OWNER_PID row)
            {
                LocalAddress = new System.Net.IPAddress(row.dwLocalAddr).ToString();
                LocalPort = (int)System.Net.IPAddress.NetworkToHostOrder((short)row.dwLocalPort);
                RemoteAddress = new System.Net.IPAddress(row.dwRemoteAddr).ToString();
                RemotePort = (int)System.Net.IPAddress.NetworkToHostOrder((short)row.dwRemotePort);
                ProcessId = (int)row.dwOwningPid;
                State = ((TcpState)row.dwState).ToString();
            }
        }

        private enum TcpState
        {
            CLOSED = 1,
            LISTEN = 2,
            SYN_SENT = 3,
            SYN_RECEIVED = 4,
            ESTABLISHED = 5,
            FIN_WAIT_1 = 6,
            FIN_WAIT_2 = 7,
            CLOSE_WAIT = 8,
            CLOSING = 9,
            LAST_ACK = 10,
            TIME_WAIT = 11,
            DELETE_TCB = 12
        }


        [StructLayout(LayoutKind.Sequential)]
        private struct UDP_TABLE_ROW
        {
            public int LocalAddr;
            public byte LocalPort1, LocalPort2, LocalPort3, LocalPort4;
            public int OwningPid;
        }

        internal static bool IsTCPPortInUse(params int[] ports)
        {
            var table = GetOpenTcpConnections().Where(conn => conn.State == "LISTEN" || conn.State == "ESTABLISHED" || conn.State == "CLOSE_WAIT");

            foreach (var entry in table)
            {
                if (ports.Contains(entry.LocalPort))
                    return true;

                //Console.WriteLine($"Local: {entry.LocalAddress}:{entry.LocalPort} -> Remote: {entry.RemoteAddress}:{entry.RemotePort} | PID: {entry.ProcessId} | State: {entry.State}");
            }

            return false;
        }

        private static List<TcpConnectionEntry> GetOpenTcpConnections()
        {
            List<TcpConnectionEntry> connections = [];

            int bufferSize = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, AF_INET, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);
            IntPtr tcpTablePtr = Marshal.AllocHGlobal(bufferSize);

            try
            {
                if (GetExtendedTcpTable(tcpTablePtr, ref bufferSize, true, AF_INET, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0) == 0)
                {
                    MIB_TCPTABLE_OWNER_PID tcpTable = Marshal.PtrToStructure<MIB_TCPTABLE_OWNER_PID>(tcpTablePtr);
                    IntPtr rowPtr = (IntPtr)((long)tcpTablePtr + Marshal.SizeOf(tcpTable.dwNumEntries));

                    for (int i = 0; i < tcpTable.dwNumEntries; i++)
                    {
                        MIB_TCPROW_OWNER_PID row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);
                        connections.Add(new TcpConnectionEntry(row));
                        rowPtr = (IntPtr)((long)rowPtr + Marshal.SizeOf(row));
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(tcpTablePtr);
            }

            return connections;
        }


        internal static bool IsUDPPortInUse(params int[] ports)
        {
            int bufferSize = 0;
            uint result = GetExtendedUdpTable(nint.Zero, ref bufferSize, true, AF_INET, UDP_TABLE_CLASS.OWNER_PID, 0);

            nint tablePtr = Marshal.AllocHGlobal(bufferSize);
            try
            {
                if (GetExtendedUdpTable(tablePtr, ref bufferSize, true, AF_INET, UDP_TABLE_CLASS.OWNER_PID, 0) != 0)
                    return false;

                int tableSize = Marshal.ReadInt32(tablePtr);
                nint rowPtr = tablePtr + 4;

                for (int i = 0; i < tableSize; i++)
                {
                    UDP_TABLE_ROW row = Marshal.PtrToStructure<UDP_TABLE_ROW>(rowPtr);

                    int localPort = row.LocalPort1 << 8 | row.LocalPort2;

                    if (ports.Contains(localPort))
                        return true;

                    rowPtr += Marshal.SizeOf<UDP_TABLE_ROW>();
                }
            }
            finally
            {
                Marshal.FreeHGlobal(tablePtr);
            }
            return false;
        }
    }
}