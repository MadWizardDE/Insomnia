using Autofac;
using MadWizard.Insomnia.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MadWizard.Insomnia
{
    public class ActionManager(IOptions<InsomniaConfig> config) : IStartable
    {
        public required ILogger<ActionManager> Logger { protected get; init; }

        private readonly List<Actor> _actors = [];

        public required IEnumerable<Actor> InjectableActors { private get; init; }

        void IStartable.Start()
        {
            foreach (var group in config.Value.ActionGroup)
            {
                RegisterActor(new ActionGroup(group));
            }

            foreach (var actor in InjectableActors)
            {
                RegisterActor(actor);
            }
        }

        public void RegisterActor(Actor actor)
        {
            if (actor == null)
                throw new ArgumentNullException(nameof(actor));

            _actors.Add(actor);
        }

        public bool TryHandleEventAction(Event eventObj, NamedAction action)
        {
            foreach (var actor in _actors)
            {
                try
                {
                    if (actor.TryHandleEventAction(eventObj, action))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    HandleActionError(new ActionError(eventObj, action, ex) { Actor = actor });

                    return true;
                }
            }

            return false;
        }

        public bool HandleActionError(ActionError error)
        {
            string postfix = ":";
            if (error.Actor != null && error.Event.Source != error.Actor)
                postfix = $" @ {error.Actor.GetType().Name}:";

            Logger.LogError(error.Exception, $"{error.Event} -> {error.Action}" + postfix);

            return true;
        }
    }
}
