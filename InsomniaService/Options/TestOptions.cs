using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service.Options
{
    // Define the 'config' verb
    [Verb("test", HelpText = "Test commands")]
    public class TestOptions
    {
        [Value(0, MetaName = "command", Required = true)]
        public required string Command { get; set; }
    }
}
