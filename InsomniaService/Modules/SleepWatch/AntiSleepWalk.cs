using Autofac;
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

        ISessionManager _sessionManager;

        ActivityDetector.SleepInhibitor _sleepInhibitor;

        bool _hibernate;

        public AntiSleepWalk(InsomniaConfig config, ISessionManager sessionManager,
            ActivityDetector.SleepInhibitor sleepInhibitor = null)
        {
            _sessionManager = sessionManager;

            _sleepInhibitor = sleepInhibitor;

            if (config.SleepWatch?.AntiSleepWalk != null)
            {
                _presenceTimer = new Timer();
                _presenceTimer.AutoReset = false;
                _presenceTimer.Interval = config.Interval;
                _presenceTimer.Elapsed += OnTimerElapsed;

                _hibernate = config.SleepWatch.SuspendTo == SleepWatchConfig.SuspendState.HIBERNATE;
            }
        }

        [Autowired]
        ILogger<AntiSleepWalk> Logger { get; set; }

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

            if (_sleepInhibitor?.Request != null)
            {
                Logger.LogInformation(InsomniaEventId.COMPUTER_BUSY, "Computer busy");

                return;
            }

            Logger.LogInformation(InsomniaEventId.USER_NOT_PRESENT, "User not present");

            Win32API.EnterStandby(_hibernate);
        }

        void Disarm()
        {
            _sessionManager.UserPresent -= OnUserDetected;
            _sessionManager.UserLogin -= OnUserDetected;

            _presenceTimer.Stop();
        }
    }
}