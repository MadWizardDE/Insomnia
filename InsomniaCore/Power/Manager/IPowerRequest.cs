using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Power.Manager
{
    public interface IPowerRequest : IDisposable
    {
        public string Name { get; }
        public string? Reason { get; }
    }
}
