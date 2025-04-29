using MadWizard.Insomnia.NetworkSession.Manager;
using Microsoft.Extensions.Logging;
using Microsoft.Management.Infrastructure;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MadWizard.Insomnia.NetworkSession.Manager
{
    internal class CIMNetworkSessionManager(CIMNetworkFileManager fileManager) : CIMSMBManager<ulong, CIMNetworkSession>("MSFT_SmbSession"), INetworkSessionManager
    {
        public required ILogger<CIMNetworkSessionManager> Logger { private get; init; }

        private readonly Dictionary<string, NetworkClient> _clients = [];

        public IEnumerator<INetworkSession> GetEnumerator() => Objects.Values.GetEnumerator();

        protected override ISet<ulong> RefreshObjects(Dictionary<ulong, CIMNetworkSession> objects)
        {
            var existingIDs = new HashSet<ulong>();

            foreach (var instance in Instances)
            {
                var id = (UInt64)instance.CimInstanceProperties["SessionId"].Value;

                existingIDs.Add(id);

                if (!objects.TryGetValue(id, out var session))
                {
                    var computerName = (String)instance.CimInstanceProperties["ClientComputerName"].Value;

                    if (!_clients.TryGetValue(computerName, out var client))
                    {
                        var dialect = (String)instance.CimInstanceProperties["Dialect"].Value;

                        _clients[computerName] = client = new NetworkClient(computerName, dialect);
                    }

                    objects[id] = session = new CIMNetworkSession(id)
                    {
                        Client = client,

                        Files = fileManager.Where(file => file.SessionId == id)
                    }; ;
                }

                session.UpdateProperties(instance);
            }

            return existingIDs;
        }
    }
}
