using MadWizard.Insomnia.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Timers;
using static System.Collections.Specialized.BitVector32;

namespace MadWizard.Insomnia
{
    /**
     * Base class for a ressource that can be monitored for usage. A ressource is an entity, that must be identifiable unambiguously.
     * 
     * Every ressource can be configured with an idle action that is triggered when it is detected, that the ressource is no longer in use.
     * 
     * Actions can be triggered manually or scheduled to be triggered after a certain delay.
     */
    public abstract class Resource : Actor, IInspectable
    {
        internal readonly ISet<Resource> Monitors = new HashSet<Resource>();

        public bool IsTrackedBy(object monitor) => Monitors.Contains(monitor);
        public bool IsTrackedBy<T>() => Monitors.OfType<T>().Any();

        public event EventInvocation? Idle;
        public event EventInvocation? Usage;

        public event EventInvocation? Inspection;

        public virtual IEnumerable<UsageToken> Inspect(TimeSpan interval)
        {
            Stopwatch watch = new();

            watch.Start();
            var tokens = InspectResource(interval).ToArray();
            watch.Stop();

            if (tokens.Length == 0)
            {
                CancelEventAction(nameof(Usage));

                TriggerEvent(new ResourceInspectionEvent(nameof(Idle)) { Duration = watch.Elapsed, Tokens = tokens });
            }
            else
            {
                CancelEventAction(nameof(Idle));

                TriggerEvent(new ResourceInspectionEvent(nameof(Usage)) { Duration = watch.Elapsed, Tokens = tokens });
            }

            TriggerEvent(new ResourceInspectionEvent(nameof(Inspection)) { Duration = watch.Elapsed, Tokens = tokens });

            return tokens;
        }

        protected abstract IEnumerable<UsageToken> InspectResource(TimeSpan interval);


        #region Action/Error-Bubbling
        protected override bool HandleEventAction(Event eventObj, NamedAction action)
        {
            if (!base.HandleEventAction(eventObj, action))
            {
                foreach (var monitor in Monitors)
                    if (monitor.HandleEventAction(eventObj, action))
                        return true;

                return false;
            }

            return true;
        }

        protected override bool HandleActionError(ActionError error)
        {
            if (!base.HandleActionError(error))
            {
                foreach (var monitor in Monitors)
                    if (monitor.HandleActionError(error))
                        return true;
            }

            return false;
        }
        #endregion
    }
}
