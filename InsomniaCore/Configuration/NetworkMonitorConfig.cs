using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MadWizard.Insomnia.Configuration;

namespace MadWizard.Insomnia.Configuration
{
    public interface INetworkInterfaceConfig
    {
        string Name { get; }

        string Interface { get; }
    }

    public class NetworkMonitorConfig : INetworkInterfaceConfig
    {
        public required string Name { get; set; }
        public required string Interface { get; set; }

        public IList<TCPServiceInfo> TCPService { get; private set; } = [];
        public IList<HTTPServiceInfo> HTTPService { get; private set; } = [];

        public IList<NetworkHostInfo> PingHost { get; private set; } = [];
        public IList<VirtualHostInfo> VirtualHost { get; private set; } = [];
    }

    public class NetworkHostInfo
    {
        public required string Name { get; set; }

        private string? MAC { get; set; }
        private string? IPv4 { get; set; }

        public PhysicalAddress? PhysicalAddress => this.MAC != null ? PhysicalAddress.Parse(this.MAC) : null;
        public IPAddress? IPv4Address => this.IPv4 != null ? IPAddress.Parse(this.IPv4) : null;

    }

    public class VirtualHostInfo : NetworkHostInfo
    {
        public NamedAction? OnAccess { get; set; } = new NamedAction("start");
        public DelayedAction? OnIdle { get; set; }

        public IList<TCPServiceInfo> TCPService { get; private set; } = [];
        public IList<HTTPServiceInfo> HTTPService { get; private set; } = [];
    }

    public class TCPServiceInfo
    {
        public required string Name { get; set; }
        public required int Port { get; set; }
        public int Threshold { get; set; } = 1;
    }

    public class HTTPServiceInfo : TCPServiceInfo
    {
        public HTTPServiceInfo()
        {
            Port = 80;
        }

        public IList<HTTPRequestInfo> Request { get; private set; } = [];
    }

    public class HTTPRequestInfo
    {
        public required string Name { get; set; }

        public required string? Path { get; set; }

        public bool Ignore { get; set; } = false;
    }
}
