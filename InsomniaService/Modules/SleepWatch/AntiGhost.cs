using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Lifetime;
using MadWizard.Insomnia.Service.Sessions;
using MadWizard.Insomnia.Tools;
using Microsoft.Extensions.Logging;
using System;
using System.ServiceProcess;
using System.Timers;

namespace MadWizard.Insomnia.Service.SleepWatch
{
    class AntiGhost : ISessionChangeHandler
    {
        Timer _delayTimer;

        ISessionManager _sessionManager;

        ActivityDetector.SleepInhibitor _sleepInhibitor;

        bool _hibernate;

        public AntiGhost(InsomniaConfig config, ISessionManager sessionManager,
            ActivityDetector.SleepInhibitor sleepInhibitor = null)
        {
            _sessionManager = sessionManager;

            _sleepInhibitor = sleepInhibitor;

            if (config.SleepWatch?.AntiGhost != null)
            {
                _delayTimer = new Timer();
                _delayTimer.AutoReset = false;
                _delayTimer.Interval = config.Interval;
                _delayTimer.Elapsed += OnTimerElapsed;

                _hibernate = config.SleepWatch.SuspendTo == SleepWatchConfig.SuspendState.HIBERNATE;
            }
        }

        [Autowired]
        ILogger<AntiGhost> Logger { get; set; }

        void ISessionChangeHandler.OnSessionChange(SessionChangeDescription desc)
        {
            if (desc.Reason == SessionChangeReason.RemoteDisconnect)
            {
                _delayTimer.Stop();
                _delayTimer.Start();
            }
        }

        void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            Logger.LogInformation($"Checking for active session...");

            foreach (Session session in _sessionManager.Sessions)
            {
                if (session.ConnectionState == ConnectionState.Active)
                {
                    Logger.LogInformation(InsomniaEventId.USER_PRESENT, $"User {session.UserName} is present");

                    return;
                }
            }

            if (_sleepInhibitor?.Request != null)
            {
                Logger.LogInformation(InsomniaEventId.COMPUTER_BUSY, "Computer busy");

                return;
            }

            Logger.LogInformation(InsomniaEventId.USER_NOT_PRESENT, "User not present");

            Win32API.EnterStandby(_hibernate);
        }
    }
}