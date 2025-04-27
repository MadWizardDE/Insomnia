using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.NetworkSession.Manager
{
    public interface INetworkSession
    {
        string UserName { get; }

        string? ComputerName { get; }
        IPAddress? IPAddress { get; }

        int? NumberOfOpenFiles { get; }

        public TimeSpan ConnectionTime { get; }
        public TimeSpan IdleTime { get; }

        bool IsGuest { get; }

    }
}
