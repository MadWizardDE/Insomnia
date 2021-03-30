using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.SleepWatch;
using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.SleepWatch.Detector
{
    class ManualOverrideSwitch : ActivityDetector.IDetector
    {
        bool _active = false;
        bool _allowUser = false;

        public ManualOverrideSwitch(InsomniaConfig config)
        {
            var overrideConfig = config.SleepWatch.ActivityDetector.ManualOverride;

            _allowUser = overrideConfig.AllowUser;
        }

        public bool IsUserAllowed => _allowUser;

        public bool Enabled
        {
            get => _active;

            set
            {
                if (_active != value)
                {
                    _active = value;

                    SwitchStateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler SwitchStateChanged;

        (string[] tokens, bool busy) ActivityDetector.IDetector.Scan()
        {
            return (Enabled ? new string[] { "Sleepless" } : Array.Empty<string>(), Enabled);
        }
    }
}
