using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.NetworkSession.Manager
{
    public interface INetworkClient
    {
        string? Name { get; }

        IPAddress? Address { get; }

        string? ProtocolVersion { get; }
    }
}
