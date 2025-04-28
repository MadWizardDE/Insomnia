using MadWizard.Insomnia.Configuration;

namespace MadWizard.Insomnia.Service.Bridge.Configuration
{
    public class BridgedSessionManagerConfig : SessionConfig<BridgedSessionDescriptor>
    {
        public bool? SpawnMinions { get; set; } = true;
    }

    public class BridgedSessionDescriptor : SessionDescriptor
    {
        public SessionMatcher? AllowControlSession { get; set; }
        public bool? AllowControlSleep { get; set; }
    }
}
