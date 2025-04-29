﻿using MadWizard.Insomnia.NetworkSession.Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.NetworkSession.Manager
{
    internal class NetworkClient(string computerName, string protocolVersion) : INetworkClient
    {
        public string Name
        {
            get
            {
                try
                {
                    var ip = IPAddress.Parse(computerName);

                    if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                        return "[IPv6]";

                    try
                    {
                        IPHostEntry entry = Dns.GetHostEntry(ip);

                        var parts = entry.HostName.Split("."); // PARALLELWELT[.fritz.box]

                        return parts[0];
                    }
                    catch (SocketException)
                    {
                        return computerName;
                    }
                }
                catch (FormatException)
                {
                    return computerName;
                }
            }
        }

        public IPAddress? Address
        {
            get
            {
                try
                {
                    return IPAddress.Parse(computerName);
                }
                catch (FormatException)
                {
                    return null;
                }
            }
        }

        public string? ProtocolVersion => protocolVersion;
    }
}
