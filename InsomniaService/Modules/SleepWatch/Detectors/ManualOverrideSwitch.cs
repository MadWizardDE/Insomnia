using MadWizard.Insomnia.Service.SleepWatch;
using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.SleepWatch.Detector
{
    class ManualOverrideSwitch : ActivityDetector.IDetector
    {
        bool _active = false;

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
