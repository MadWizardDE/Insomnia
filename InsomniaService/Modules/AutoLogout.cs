using Autofac;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Sessions;
using MadWizard.Insomnia.Service.SleepWatch;
using MadWizard.Insomnia.Service.SleepWatch.Detector;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;
using static MadWizard.Insomnia.Configuration.AutoLogoutConfig;
using static MadWizard.Insomnia.Configuration.AutoLogoutConfig.UserInfo;
using static MadWizard.Insomnia.Configuration.SleepWatchConfig.ActivityDetectorConfig.PowerRequestConfig;

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
        Timer _exceptionTimer;

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
            InstallExceptionTimer();

            _logger = logger;

            _sessionManager = sessionManager;
        }

        [Autowired]
        PowerRequestDetector PowerRequestDetector { get; set; }

        private void InstallExceptionTimer()
        {
            var shortestTimeout = _notifyTimeSpan;

            foreach (UserInfo userInfo in _config.User.Values)
                if (userInfo.TimeoutSpan < shortestTimeout)
                    shortestTimeout = userInfo.TimeoutSpan;

            _exceptionTimer = new Timer();
            _exceptionTimer.AutoReset = true;
            _exceptionTimer.Interval = (shortestTimeout / 2).TotalMilliseconds;
            _exceptionTimer.Elapsed += OnExceptionTimerElapsed;
            _exceptionTimer.Start();
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

        private void OnExceptionTimerElapsed(object sender, ElapsedEventArgs args)
        {
            bool ShouldSuspendLogout(UserInfo user)
            {
                foreach (LogoutExceptionInfo exceptionInfo in user.LogoutException.Values)
                {
                    switch (exceptionInfo.Type)
                    {
                        case LogoutExceptionInfo.LogoutExceptionType.REQUEST:
                            lock (PowerRequestDetector.LastRequests)
                                foreach (RequestInfo request in PowerRequestDetector.LastRequests)
                                    if (request.Name.Equals(exceptionInfo.Text))
                                    {
                                        _logger.LogInformation($"LogoutException['{exceptionInfo.Name}']: " +
                                            $"PowerRequest '{request.Name}' present. " +
                                            $"Suspending logout for {user.Name}.");

                                        return true;
                                    }

                            break;
                    }
                }

                return false;
            }

            lock (this)
            {
                foreach (string userName in _idleTimers.Keys)
                {
                    if (ShouldSuspendLogout(_config.User[userName]))
                    {
                        RestartTimer(userName);
                    }
                }
            }
        }

        private void OnNotifyTimerElapsed(object sender, ElapsedEventArgs args)
        {
            lock (this)
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
            lock (this)
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
            lock (this)
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
        }

        private void StartTimer(string userName, int timeout)
        {
            lock (this)
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

        private void RestartTimer(string userName)
        {
            if (_idleTimers.TryGetValue(userName, out Timer idleTimer))
            {
                idleTimer.Stop();
                idleTimer.Start();
            }

            if (_notifyTimers.TryGetValue(userName, out Timer notifyTimer))
            {
                notifyTimer.Stop();
                notifyTimer.Start();
            }
        }

        private bool StopTimer(string userName, bool elapsed = false)
        {
            lock (this)
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