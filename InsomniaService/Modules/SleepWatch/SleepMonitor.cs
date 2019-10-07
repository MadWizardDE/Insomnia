using MadWizard.Insomnia.Service.Lifetime;
using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;

namespace MadWizard.Insomnia.Service.SleepWatch
{
    class SleepMonitor : IPowerEventHandler
    {
        public DateTime? SuspendTime { get; private set; }

        public TimeSpan Duration { get; private set; }

        public event EventHandler<PowerNapEventArgs> PowerNap;
        public event EventHandler<EventArgs> SleepOver;

        public void ResetTime()
        {
            Duration = TimeSpan.Zero;
        }

        public void OnPowerEvent(PowerBroadcastStatus status)
        {
            switch (status)
            {
                case PowerBroadcastStatus.Suspend:
                    SuspendTime = DateTime.Now;
                    break;

                case PowerBroadcastStatus.ResumeSuspend:
                    if (SuspendTime.HasValue)
                    {
                        DateTime suspend = SuspendTime.Value;

                        if (DateTime.Now.Day != suspend.Day)
                        {
                            Duration += DateTime.Today - suspend;
                            SleepOver?.Invoke(this, EventArgs.Empty);
                            Duration += DateTime.Now - DateTime.Today;
                        }
                        else
                        {
                            TimeSpan time = DateTime.Now - suspend;
                            PowerNap?.Invoke(this, new PowerNapEventArgs { SleepTime = time });
                            Duration += time;
                        }

                        SuspendTime = null;
                    }
                    break;
            }
        }
    }

    class PowerNapEventArgs : EventArgs
    {
        public TimeSpan SleepTime { get; internal set; }
    }
}