using Autofac.Core;
using MadWizard.Insomnia.Configuration;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Network.Services
{
    public class TCPService : NetworkService
    {
        public int Port { get; private set; }

        public TCPService(TCPServiceInfo info) : base(info.Name)
        {
            Port = info.Port;

            AccessThreshold = info.Threshold;
        }

        public override bool Accepts(Packet packet)
        {
            return packet is TcpPacket tcp && tcp.DestinationPort == this.Port;
        }

        public override string ToString() => $"{Name}@tcp/{Port}";

    }
}
