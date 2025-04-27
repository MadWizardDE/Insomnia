using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ActionHandlerAttribute(string name) : Attribute
    {
        public string Name => name;
    }
}
