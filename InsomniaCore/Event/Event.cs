using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia
{
    public class Event(string type, EventOptions options = default)
    {
        private readonly ISet<object> _contexts = new HashSet<object>();

        public string Type => type;

        public EventSource? Source { get; internal set; }

        public EventOptions Options => options;

        public IEnumerable<object> Context
        {
            get
            {
                yield return this;

                if (Source != null)
                    yield return Source;

                foreach (var context in _contexts)
                {
                    yield return context;
                }
            }
        }

        public void AddContext(object context)
        {
            ArgumentNullException.ThrowIfNull(context);

            _contexts.Add(context);
        }

        public override string ToString() => $"{GetType().Name}('{Type}' at {Source?.GetType().Name ?? "???"})";
    }
}
