using MadWizard.Insomnia.Configuration;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia
{
    public class EventType(EventInfo type, FieldInfo field)
    {
        public string Name => type.Name;

        public void AddEventHandler(EventSource source, EventInvocation handler)
        {
            type.GetAddMethod(true)?.Invoke(source, [handler]);

            //type.AddEventHandler(source, handler);
        }

        public void RemoveEventHandler(EventSource source, EventInvocation handler)
        {
            type.GetRemoveMethod(true)?.Invoke(source, [handler]);

            //type.RemoveEventHandler(source, handler);
        }

        public Delegate[] GetInvocationList(EventSource source)
        {
            return field.GetValue(source) is Delegate gate ? gate.GetInvocationList() : [];
        }

    }
}
