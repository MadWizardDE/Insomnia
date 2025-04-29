using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace MadWizard.Insomnia.Configuration
{
    public class NetworkSessionMonitorConfig
    {
        public IEnumerable<NetworkSessionFilterRule> FilterRule { get; set; } = [];
    }

    public class NetworkSessionFilterRule
    {
        public string? UserName { get; set; }

        public string? ClientName { get; set; }
        public string? ClientIP { get; set; }

        public string? ShareName { get; set; }
        public string? FilePath { get; set; }

        public IPAddress? ClientIPAddress => ClientIP != null ? IPAddress.Parse(ClientIP) : null;
        public Regex? FilePathPattern => FilePath != null ? new Regex(FilePath) : null;

        public FilterType Type { get; set; } = FilterType.Exclude;
    }

    public enum FilterType
    {
        Exclude = 0,
        Include
    }
}
