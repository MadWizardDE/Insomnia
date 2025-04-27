using MadWizard.Insomnia.Configuration;

namespace MadWizard.Insomnia
{
    class ActionGroup(ActionGroupConfig config) : Actor
    {
        protected override bool HandleEventAction(Event eventObj, NamedAction action)
        {
            return false; // TODO
        }
    }
}
