using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.NetworkSession.Manager
{
    public interface INetworkShare
    {
        string Name { get; }

        string Description { get; }

        string Path { get; }

        IEnumerable<INetworkFile> Files { get; }

    }
}
