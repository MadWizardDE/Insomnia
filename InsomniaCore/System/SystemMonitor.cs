using Autofac;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Power.Manager;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Timer = System.Timers.Timer;

namespace MadWizard.Insomnia
{
    public class SystemMonitor(IOptions<InsomniaConfig> config, IPowerManager power, ActionManager actionManager) : ResourceMonitor<IInspectable>, IStartable, IDisposable
    {
        public required ILogger<SystemMonitor> Logger { protected get; init; }

        public required IEnumerable<IInspectable> AvailableMonitors { private get; init; }

        private IPowerRequest? Request { get; set; }

        private DateTime? _sleeplessUntil = null;
        private bool? _sleeplessIfUsage = config.Value.OnUsage?.Name == "sleepless";

        private Timer? _sleeplessTimer;

        public bool Sleepless
        {
            get => _sleeplessUntil != null && _sleeplessUntil > DateTime.Now;

            set => SleeplessUntil = value ? DateTime.MaxValue : null;
        }

        public bool? SleeplessIfUsage
        {
            get => _sleeplessIfUsage;

            set
            {
                _sleeplessIfUsage = value;

                SleeplessChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public DateTime? SleeplessUntil
        {
            get => _sleeplessUntil;

            set
            {
                _sleeplessTimer?.Dispose();
                _sleeplessTimer = null;

                _sleeplessUntil = value;

                SleeplessChanged?.Invoke(this, EventArgs.Empty);

                if (_sleeplessUntil != null && _sleeplessUntil != DateTime.MaxValue)
                {
                    _sleeplessTimer = new Timer(_sleeplessUntil.Value - DateTime.Now);
                    _sleeplessTimer.Elapsed += (sender, args) => SleeplessUntil = null;
                    _sleeplessTimer.AutoReset = false;
                    _sleeplessTimer.Start();
                }
            }
        }

        public event EventHandler? SleeplessChanged;

        void IStartable.Start()
        {
            AddEventAction(nameof(Idle), config.Value.OnIdle);
            AddEventAction(nameof(Usage), config.Value.OnUsage);

            foreach (var monitor in AvailableMonitors.Where(that => that != this))
                this.StartTracking(monitor);

            power.Suspended += PowerManager_Suspended;
        }

        private void PowerManager_Suspended(object? sender, EventArgs e)
        {
            CancelEventAction(nameof(Idle));
        }

        public override IEnumerable<UsageToken> Inspect(TimeSpan interval)
        {
            ClearSleepless();

            return base.Inspect(interval);
        }

        protected override IEnumerable<UsageToken> InspectResource(TimeSpan interval)
        {
            if (Sleepless)
                yield return new SleeplessToken();

            foreach (var token in base.InspectResource(interval))
                yield return token;
        }

        [ActionHandler("reboot")]
        internal void HandleActionReboot() => power.Reboot();
        [ActionHandler("shutdown")]
        internal void HandleActionShutdown() => power.Shutdown();
        [ActionHandler("sleep")]
        internal void HandleActionSleep() => power.Suspend();

        [ActionHandler("sleepless")]
        internal void HandleActionSleepless(ResourceInspectionEvent eventRef, string? reason = null, bool addTokens = true)
        {
            if (reason == null)
            {
                reason = $"No Standby because: " + (eventRef.Tokens.Any() ? string.Join(", ", eventRef.Tokens) : "?");
            }

            if (SleeplessIfUsage != false || eventRef.Tokens.OfType<SleeplessToken>().Any())
            {
                Request = power.CreateRequest($"{reason}");
            }
        }

        protected override bool HandleEventAction(Event eventObj, NamedAction action)
        {
            if (!base.HandleEventAction(eventObj, action))
            {
                if (actionManager.TryHandleEventAction(eventObj, action))
                    return true;

                return false;
            }

            return true;
        }

        protected override bool HandleActionError(ActionError error)
        {
            return actionManager.HandleActionError(error);
        }

        private void ClearSleepless()
        {
            Request?.Dispose();
            Request = null;
        }

        public override void Dispose()
        {
            ClearSleepless();

            base.Dispose();
        }
    }

    public class SleeplessToken : UsageToken
    {
        public override string ToString() => "Sleepless";
    }
}
