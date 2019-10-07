using Autofac.Features.OwnedInstances;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Lifetime;
using MadWizard.Insomnia.Service.Sessions;
using MadWizard.Insomnia.Tools;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;
using System.Timers;

namespace MadWizard.Insomnia.Service.SleepWatch
{
    class AntiSleepWalk : IPowerEventHandler
    {
        Timer _presenceTimer;

        SessionManager _sessionManager;

        ActivityDetector.SleepInhibitor _sleepInhibitor;

        public AntiSleepWalk(InsomniaConfig config, SessionManager sessionManager,
            ActivityDetector.SleepInhibitor sleepInhibitor)
        {
            _sessionManager = sessionManager;
            _sleepInhibitor = sleepInhibitor;

            if (config.SleepWatch?.AntiSleepWalk != null)
            {
                _presenceTimer = new Timer();
                _presenceTimer.AutoReset = false;
                _presenceTimer.Interval = config.Interval;
                _presenceTimer.Elapsed += OnTimerElapsed;
            }
        }

        [Autowired]
        ILogger<NetworkCommander> Logger { get; set; }

        void IPowerEventHandler.OnPowerEvent(PowerBroadcastStatus status)
        {
            switch (status)
            {
                case PowerBroadcastStatus.Suspend:
                    Disarm();
                    break;

                case PowerBroadcastStatus.ResumeSuspend:
                    _presenceTimer.Start();

                    _sessionManager.UserLogin += OnUserDetected;

                    if (_sessionManager.ConsoleActive && !_sessionManager.ConsoleLocked)
                    {
                        //= keine Kennworteingabe nach dem Aufwachen

                        _sessionManager.UserPresent += OnUserDetected;
                    }

                    break;
            }
        }

        void OnUserDetected(object sender, EventArgs e)
        {
            if (_presenceTimer.Enabled)
            {
                Disarm();

                Logger.LogInformation(InsomniaEventId.USER_PRESENT, "User present");
            }
        }
        void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            Disarm();

            if (_sleepInhibitor.Request != null)
            {
                Logger.LogInformation(InsomniaEventId.COMPUTER_BUSY, "Computer busy");

                return;
            }

            Logger.LogInformation(InsomniaEventId.USER_NOT_PRESENT, "User not present");

            Win32API.EnterStandby();
        }

        void Disarm()
        {
            _sessionManager.UserPresent -= OnUserDetected;
            _sessionManager.UserLogin -= OnUserDetected;

            _presenceTimer.Stop();
        }
    }
}