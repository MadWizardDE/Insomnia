using MadWizard.Insomnia.Configuration;
using PacketDotNet;
using System.Text;
using System.Text.RegularExpressions;

namespace MadWizard.Insomnia.Network.Services
{
    public class HTTPService(HTTPServiceInfo info) : TCPService(info)
    {
        public override void AccessBy(Packet? packet, bool remember = true)
        {
            if (packet is TcpPacket tcp)
            {
                var http = ParseRequest(tcp);

                if (remember && http != null)
                {
                    bool? shouldRemember = null;

                    foreach (var request in info.Request)
                    {
                        if (request.Path != null && http.Target.Contains(request.Path))
                        {
                            shouldRemember = !request.Ignore;
                        }
                    }

                    base.AccessBy(packet, shouldRemember ?? ShouldRememberByDefault());
                }
                else if (tcp.Synchronize)
                {
                    base.AccessBy(packet, false);
                }
            }
        }

        private bool ShouldRememberByDefault()
        {
            if (info.Request.Count == 0)
                return true;

            // has only ignore?
            if (info.Request.Aggregate(true, (i, r) => i && r.Ignore))
                return true;

            return false;
        }

        private static HTTPRequest? ParseRequest(TcpPacket tcp)
        {
            if (tcp.PayloadData.Length > 0)
                try
                {
                    string text = Encoding.Default.GetString(tcp.PayloadData);
                    string[] lines = Regex.Split(text, "\r\n|\r|\n");
                    string first = lines[0];
                    string[] parts = first.Split(' ');

                    if (parts.Length > 2)
                    {
                        return new HTTPRequest()
                        {
                            Method = parts[0],
                            Target = parts[1],
                            Version = parts[2]
                        };
                    }
                }
                catch
                {

                }

            return null;
        }

        internal class HTTPRequest
        {
            public required string Method { get; init; }

            public required string Target { get; init; }

            public required string Version { get; init; }
        }
    }
}
