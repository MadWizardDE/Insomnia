using MadWizard.Insomnia.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia
{
    public abstract class EventSource
    {
        private readonly Dictionary<string, EventType> _events = [];

        protected EventSource()
        {
            foreach (var eventInfo in GetType().GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (eventInfo.EventHandlerType?.Name.Contains("EventInvocation") ?? false)
                {
                    var fieldInfo = GetType().GetAllFields(BindingFlags.NonPublic | BindingFlags.Instance).Where(f => f.Name == eventInfo.Name).First();

                    if (fieldInfo != null)
                    {
                        EventType type = new(eventInfo, fieldInfo);

                        _events.Add(eventInfo.Name, type);
                    }
                }
            }
        }

        protected EventType EventTypeByName(string eventName)
        {
            return _events[eventName];
        }

        public void AddEventHandler(string eventName, EventInvocation handler)
        {
            _events[eventName].AddEventHandler(this, handler);
        }

        public void RemoveEventHandler(string eventName, EventInvocation handler)
        {
            _events[eventName].RemoveEventHandler(this, handler);
        }

        protected void TriggerEvent(string eventName)
        {
            TriggerEvent(new Event(eventName));
        }

        protected void TriggerEvent(Event eventRef)
        {
            if (_events.TryGetValue(eventRef.Type, out var type))
            {
                eventRef.Source = this;

                foreach (var context in Context)
                {
                    eventRef.AddContext(context);
                }

                foreach (var del in type.GetInvocationList(this))
                {
                    del.DynamicInvoke(eventRef);
                }
            }
        }

        private IEnumerable<object> Context
        {
            get
            {
                foreach (var property in GetType().GetAllProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(f => f.GetCustomAttribute<EventContextAttribute>() != null))
                {
                    var context = property.GetValue(this);

                    if (context != null)
                    {
                        yield return context;
                    }
                }
            }
        }
    }
}
