using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia
{
    public class ResourceMonitor<T> : Resource, IIEnumerable<T> where T : IInspectable
    {
        public event Func<T, bool>? Filters;

        private readonly ISet<T> _resources = new HashSet<T>();

        private bool ShouldTrackRessource(T ressource)
        {
            if (Filters != null)
            {
                foreach (Func<T, bool> filter in Filters.GetInvocationList().Cast<Func<T, bool>>())
                    if (!filter(ressource))
                        return false;
            }

            return true;
        }

        public virtual bool StartTracking(T resource)
        {
            if (ShouldTrackRessource(resource))
            {
                if (resource is Resource r)
                    r.Monitors.Add(this);

                return _resources.Add(resource);
            }

            return false;
        }

        public virtual void StopTracking(T resource)
        {
            if (resource is Resource r)
                r.Monitors.Remove(this);

            _resources.Remove(resource);
        }

        protected virtual bool ShouldInspectResource(T resource) => true;

        protected override IEnumerable<UsageToken> InspectResource(TimeSpan interval)
        {
            foreach (var resource in this)
                if (ShouldInspectResource(resource))
                    foreach (var token in resource.Inspect(interval))
                        yield return token;
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => _resources.GetEnumerator();
    }
}
