using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia
{
    public delegate void EventInvocation(Event data);

    public delegate void EventInvocation<T>(T data) where T : Event;
}
