using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Configuration
{
    public class ProcessMonitorConfig
    {
        public IList<ProcessGroupInfo> Process { get; set; } = [];
    }

    public class ProcessGroupInfo
    {
        public required string Name { get; set; }

        public Regex Pattern => Text != null ? new Regex(Text) : throw new ArgumentNullException("pattern");

        public bool WithChildren { get; set; } = false;

        public double Threshold { get; set; }

        private string? Text { get; set; }
    }
}
