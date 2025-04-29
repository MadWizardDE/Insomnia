using MadWizard.Insomnia.NetworkSession.Manager;
using Microsoft.Management.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.NetworkSession.Manager
{
    internal class CIMNetworkShare(string name, string path) : INetworkShare
    {
        public string Name => name;
        public string Path => path;

        public string Description { get; internal set; } = "";

        public required IEnumerable<INetworkFile> Files { get; internal init; }

        internal void UpdateProperties(CimInstance instance)
        {
            if (instance.CimInstanceProperties["Description"].Value is String description)
                Description = description;
        }
    }
}
