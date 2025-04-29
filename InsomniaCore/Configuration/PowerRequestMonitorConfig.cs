using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Configuration
{
    public class PowerRequestMonitorConfig
    {
        public IList<PowerRequestInfo> Request { get; set; } = [];
        public IList<PowerRequestInfo> RequestFilter { get; set; } = [];
    }

    public class PowerRequestInfo
    {
        public required string Name { get; set; }

        private string? Text { get; set; }

        public Regex Pattern => Text != null ? new Regex(Text) : throw new ArgumentNullException("pattern");
    }
}
