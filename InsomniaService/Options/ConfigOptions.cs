using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service.Options
{
    [Verb("config", HelpText = "Configuration commands")]
    public class ConfigOptions
    {
        [Value(0, MetaName = "action", Required = true, HelpText = "Action to perform (e.g., init).")]
        public required string Action { get; set; }

        [Value(1, MetaName = "file", Required = false, HelpText = "Path to configuration *.ini-file.")]
        public required string IniFilePath { get; set; }

    }
}
