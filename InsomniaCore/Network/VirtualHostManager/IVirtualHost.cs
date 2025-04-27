using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Network
{
    public interface IVirtualHost
    {
        public VirtualHostState State { get; }

        public PhysicalAddress Address { get; }

        public Task Start();
        public Task Suspend();
        public Task Stop();

    }

    public enum VirtualHostState
    {
        Unknown = 0,

        Running,
        Suspended,
        Stopped
    }
}
