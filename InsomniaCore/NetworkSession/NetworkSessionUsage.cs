using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.NetworkSession
{
    public class NetworkSessionUsage(IPAddress? ip, string? host, string user, int? numOpenFiles = null) : UsageToken
    {
        public string? HostName => host; public string? UserName => user;

        public int? NumOpenFiles => numOpenFiles;

        public override string ToString() => @$"\\{host ?? (ip != null ? ip.ToString() : "?")}\{user}"; // + (numOpenFiles != null ? $"[{numOpenFiles}]" : string.Empty);
    }
}
