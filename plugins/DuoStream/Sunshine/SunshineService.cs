using MadWizard.Insomnia.Network;
using MadWizard.Insomnia.Network.Services;
using MadWizard.Insomnia.Service.Duo.Sunshine;
using PacketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


namespace MadWizard.Insomnia.Service.Duo.Sunshine
{
    internal class SunshineService(int port) : NetworkService($"Sunshine:{port}")
    {
        // TCP
        protected int PortHTTP => port;
        protected int PortHTTPS => port - 5;
        protected int PortWeb => port + 1;
        protected int PortRTSP => port + 21;

        // UDP
        protected int PortVideo => port + 9;
        protected int PortControl => port + 10;
        protected int PortAudio => port + 11;
        protected int PortMic => port + 13;

        public override bool IsHidden => true;

        public bool IsWaitingForClient => PortChecker.IsTCPPortInUse(PortHTTP);
        public bool HasClientConnected => PortChecker.IsUDPPortInUse(PortVideo, PortControl, PortAudio, PortMic);

        public override bool Accepts(Packet packet)
        {
            // TCP
            if (packet is TcpPacket tcp &&
                (tcp.DestinationPort == PortHTTP // HTTP
                || tcp.DestinationPort == PortHTTPS // HTTPS
                || tcp.DestinationPort == PortWeb // Web
                || tcp.DestinationPort == PortRTSP)) // RTSP
                return true;

            // UDP
            if (packet is UdpPacket udp &&
                (udp.DestinationPort == PortVideo // Video
                || udp.DestinationPort == PortControl // Control
                || udp.DestinationPort == PortAudio // Audio
                || udp.DestinationPort == PortMic)) // Mic (unused)
                return true;

            return false;
        }
    }
}

