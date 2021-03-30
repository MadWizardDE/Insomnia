using Autofac;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Sessions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;
using static MadWizard.Insomnia.Configuration.AutoLogoutConfig;

namespace MadWizard.Insomnia.Service
{
    class AutoLogout : IStartable
    {
        AutoLogoutConfig _config;

        TimeSpan _notifyTimeSpan;

        ISessionManager _sessionManager;
        ISessionService<INotificationAreaService> _sessionNotifyService;

        IDictionary<string, Timer> _idleTimers;
        IDictionary<string, Timer> _notifyTimers;

        ILogger<AutoLogout> _logger;

        public AutoLogout(InsomniaConfig config, ISessionManager sessionManager,
            Lazy<ISessionService<INotificationAreaService>> sessionNotifyService,
            ILogger<AutoLogout> logger)
        {
            _config = config.AutoLogout;
            _notifyTimeSpan = TimeSpan.FromMilliseconds(_config.NotifyTimeout);

            _sessionNotifyService = sessionNotifyService.Value;

            _idleTimers = new Dictionary<string, Timer>();
            _notifyTimers = new Dictionary<string, Timer>();

            _logger = logger;

            _sessionManager = sessionManager;
        }

        private void OnUserIdle(object sender, SessionEventArgs e)
        {
            if (_config.User.TryGetValue(e.Session.UserName, out UserInfo info))
            {
                StartTimer(e.Session.UserName, info.Timeout);

                TimeSpan idleTimeSpan = TimeSpan.FromMilliseconds(info.Timeout);
                if (idleTimeSpan <= _notifyTimeSpan)
                    notify(e.Session.Id, idleTimeSpan);
                else
                {
                    Timer notifyTimer = new Timer();
                    notifyTimer.AutoReset = false;
                    notifyTimer.Interval = (idleTimeSpan - _notifyTimeSpan).TotalMilliseconds;
                    notifyTimer.Elapsed += OnNotifyTimerElapsed;
                    notifyTimer.Start();

                    _notifyTimers[e.Session.UserName] = notifyTimer;
                }
            }
        }

        private void notify(int sessionID, TimeSpan timeSpan)
        {
            string timeString = "";
            if (timeSpan.Hours > 0)
            {
                timeString += $"{timeSpan.Hours} Stunde";

                if (timeSpan.Hours > 1)
                    timeString += $"n";
            }
            if (timeSpan.Minutes > 0)
            {
                if (timeString.Length > 0)
                    timeString += " und ";

                timeString += $"{timeSpan.Minutes} Minute";
                if (timeSpan.Minutes > 1)
                    timeString += $"n";
            }
            if (timeSpan.Hours < 1 && timeSpan.Seconds > 0)
            {
                if (timeString.Length > 0)
                    timeString += " und ";

                timeString += $"{timeSpan.Seconds} Sekunde";
                if (timeSpan.Seconds > 1)
                    timeString += $"n";
            }

            _sessionNotifyService.SelectSession(sessionID)
                .ShowNotificationAsync(
                    INotificationAreaService.NotifyMessageType.Warning,
                    "Automatische Abmeldung", $"Sie werden in {timeString} abgemeldet.");
        }

        private void OnNotifyTimerElapsed(object sender, ElapsedEventArgs args)
        {
            foreach (string userName in _notifyTimers.Keys)
            {
                if (sender == _notifyTimers[userName])
                {
                    _notifyTimers.Remove(userName);

                    ISession session = _sessionManager.FindSessionByUserName(userName);

                    if (session != null)
                    {
                        notify(session.Id, _notifyTimeSpan);
                    }

                    break;
                }
            }
        }

        private void OnIdleTimerElapsed(object sender, ElapsedEventArgs args)
        {
            foreach (string userName in _idleTimers.Keys)
            {
                if (sender == _idleTimers[userName])
                {
                    StopTimer(userName, true);

                    ISession session = _sessionManager.FindSessionByUserName(userName);

                    if (session != null)
                    {
                        _logger.LogWarning($"Logging out {session} after {_config.User[userName].Timeout} ms.");

                        _sessionManager.LogoffSession(session);
                    }

                    break;
                }
            }
        }

        private void OnUserPresent(object sender, SessionEventArgs e)
        {
            bool hasNotifyTimer = _notifyTimers.ContainsKey(e.Session.UserName);

            if (StopTimer(e.Session.UserName) && !hasNotifyTimer)
            {
                _sessionNotifyService.SelectSession(e.Session.Id)
                    .ShowNotificationAsync(
                        INotificationAreaService.NotifyMessageType.Info,
                        "Automatische Abmeldung", $"Sie bleiben angemeldet.");
            }
        }

        private void StartTimer(string userName, int timeout)
        {
            lock (_idleTimers)
            {
                if (_idleTimers.ContainsKey(userName))
                    return;

                var idleTimer = new Timer();
                idleTimer.AutoReset = false;
                idleTimer.Interval = timeout;
                idleTimer.Elapsed += OnIdleTimerElapsed;
                idleTimer.Start();

                _idleTimers.Add(userName, idleTimer);

                _logger.LogWarning($"User({userName}) will be logged out in {_config.User[userName].Timeout} ms.");
            }
        }

        private bool StopTimer(string userName, bool elapsed = false)
        {
            lock (_idleTimers)
                if (_idleTimers.TryGetValue(userName, out Timer idleTimer))
                {
                    idleTimer.Stop();

                    _idleTimers.Remove(userName);

                    if (_notifyTimers.TryGetValue(userName, out Timer notifyTimer))
                    {
                        notifyTimer.Stop();

                        _notifyTimers.Remove(userName);
                    }

                    if (!elapsed)
                        _logger.LogWarning($"User({userName}) logout out cancelled.");

                    return true;
                }

            return false;
        }

        public void Start()
        {
            _logger.LogInformation($"Sessions will be automatically logged out.");
            _sessionManager.UserIdle += OnUserIdle;
            _sessionManager.UserPresent += OnUserPresent;
        }

    }
}