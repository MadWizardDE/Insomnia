using Autofac;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Lifetime;
using MadWizard.Insomnia.Service.Sessions;
using MadWizard.Insomnia.Service.SleepWatch;
using MadWizard.Insomnia.Service.SleepWatch.Detector;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static MadWizard.Insomnia.Configuration.SleepWatchConfig;
using static MadWizard.Insomnia.Configuration.SleepWatchConfig.ActivityDetectorConfig;
using static MadWizard.Insomnia.Service.Sessions.INotificationAreaService;
using static MadWizard.Insomnia.Service.SleepWatch.NetworkCommander;
using Timer = System.Timers.Timer;

namespace MadWizard.Insomnia.Service.UI
{
    class NotificationAreaController : IStartable,
        ISessionMessageHandler<ConfigureWakeOnLANMessage>,
        ISessionMessageHandler<ConfigureWakeOptionMessage>,
        ISessionMessageHandler<ConnectToConsoleMessage>,
        ISessionMessageHandler<ConnectToRemoteMessage>,
        IDisposable

    {
        UserInterfaceConfig _configUI;
        UserInterfaceConfig.TrayMenuConfig _configTray;

        NetworkCommander _commander;
        NetworkCommanderConfig _commanderConfig;
        ManualOverrideSwitch _overrideSwitch;

        ISessionManager _sessionManager;
        ISessionService<INotificationAreaService> _sessionService;

        public NotificationAreaController(InsomniaConfig config,
            ISessionManager sessionManager, Lazy<ISessionService<INotificationAreaService>> sessionService,
            NetworkCommander commander = null, ManualOverrideSwitch overrideSwitch = null)
        {
            _configUI = config.UserInterface;
            _configTray = _configUI?.TrayMenu;

            _sessionManager = sessionManager;

            _sessionService = sessionService.Value;
            _sessionService.ServiceStarted += SessionService_Started;
            //_sessionService.ServiceStopped += SessionService_Stopped;

            if (_configTray.SessionSwitch != null)
            {
                _sessionManager.UserLogin += SessionManager_SessionChanged;
                _sessionManager.ConsoleSessionChanged += SessionManager_SessionChanged;
                _sessionManager.UserLogout += SessionManager_SessionChanged;
            }

            _commanderConfig = config.SleepWatch?.NetworkCommander;

            if (_commanderConfig != null && commander != null)
            {
                _commander = commander;
                _commander.NetworkAvailabilityChanged += NetworkCommander_NetworkAvailabilityChanged;
                _commander.NetworkTargetChanged += NetworkCommander_NetworkTargetChanged;
                _commanderConfig.ConfigChanged += NetworkCommander_ConfigChanged;
            }

            if (overrideSwitch != null)
            {
                _overrideSwitch = overrideSwitch;
                _overrideSwitch.SwitchStateChanged += ManualOverrideSwitch_StateChanged;
            }
        }

        [Autowired]
        ILogger<NotificationAreaController> Logger { get; set; }

        void IStartable.Start()
        {
            UpdateNotifyArea();
        }

        void ISessionMessageHandler<ConfigureWakeOnLANMessage>.Handle(ISession session, ConfigureWakeOnLANMessage message)
        {
            var target = _commander.GetNetworkByName(message.Target.NetworkName).GetTargetByName(message.Target.Name);

            if (message.Target.SelectedMode is string mode)
                target.WakeMode = mode;
            else if (message.Target.SelectedMode is bool wol)
                target.WakeMode = wol ? WakeModeWOL.ID : WakeModeNone.ID;
            else
                throw new ArgumentException($"Unrecognized Option: {message.Target.SelectedMode}");
        }
        void ISessionMessageHandler<ConfigureWakeOptionMessage>.Handle(ISession session, ConfigureWakeOptionMessage message)
        {
            switch (message.Option.Key)
            {
                case WakeOption.RESOLVE_IP:
                    _commanderConfig.ResolveIPAddress = (bool)message.Option.Value;
                    break;
                case WakeOption.SLEEPLESS:
                    _overrideSwitch.Enabled = (bool)message.Option.Value;
                    break;
            }
        }

        async void ISessionMessageHandler<ConnectToConsoleMessage>.Handle(ISession session, ConnectToConsoleMessage message)
        {
            ISession source, target = _sessionManager.ConsoleSession;
            if (message.User.SID > 0)
                source = _sessionManager.FindSessionByID(message.User.SID);
            else
                source = _sessionManager.FindSessionByUserName(message.User.Name);

            await ConnectToSession(session, source, target);
        }
        async void ISessionMessageHandler<ConnectToRemoteMessage>.Handle(ISession session, ConnectToRemoteMessage message)
        {
            if (session.IsRemoteConnected)
            {
                ISession source, target = session;
                if (message.User.SID > 0)
                    source = _sessionManager.FindSessionByID(message.User.SID);
                else
                    source = _sessionManager.FindSessionByUserName(message.User.Name);

                await ConnectToSession(session, source, target);
            }
        }
        async Task ConnectToSession(ISession trigger, ISession source, ISession target)
        {
            if (source != target)
            {
                Logger.LogInformation($"Connecting SID={source.Id} -> SID={(target != null ? target.Id.ToString() : "?")}...");

                bool targetConsole = target?.IsConsoleConnected ?? true;

                try
                {
                    const int NOTIFY_DELAY = 5000;

                    TimeSpan? keepPrivileges = null;
                    if (_configTray.SessionSwitch.KeepPrivileges != null && target != null && trigger != source)
                        keepPrivileges = TimeSpan.FromSeconds(_configTray.SessionSwitch.KeepPrivileges.Value);

                    _sessionManager.ConnectSession(source, target, keepPrivileges);

                    Logger.LogInformation($"ConnectToSession() successful");

                    await Task.Delay(NOTIFY_DELAY);

                    string sessionText = (targetConsole ? "Konsolen" : "Remote") + "-Sitzung";
                    string sourceText = target != null ? $" von '{target.UserName}' " : " ";
                    string infoText = $"Die {sessionText} wurde erfolgreich'{sourceText}'zu '{source.UserName}' gewechselt.";

                    if (keepPrivileges != null)
                    {
                        if (source.Security.Impersonation != null)
                        {
                            async void Security_ImpersonationChanged(object sender, EventArgs e)
                            {
                                if (sender is ISession.ISessionSecurity security && security.Impersonation == null)
                                {
                                    security.ImpersonationChanged -= Security_ImpersonationChanged;

                                    await _sessionService.SelectSession(source.Id).ShowNotificationAsync(NotifyMessageType.Warning,
                                        "Insomnia", $"Die Berechtigungen wurden wiederhergestellt.", timeout: 10000);

                                    UpdateNotifyArea(_sessionManager);
                                }
                            }

                            source.Security.ImpersonationChanged -= Security_ImpersonationChanged;
                            source.Security.ImpersonationChanged += Security_ImpersonationChanged;

                            infoText += $" Die Berechtigungen werden für {keepPrivileges} beibehalten.";
                        }
                        else
                            infoText += $" Die Berechtigungen wurden wiederhergestellt.";
                    }

                    foreach (var svcRef in _sessionService)
                        if (targetConsole || svcRef.Session == source)
                            await svcRef.Service.ShowNotificationAsync(NotifyMessageType.Info, "Insomnia", infoText, timeout: 20000);
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "ConnectToSession() failed");

                    await _sessionService.SelectSession(trigger.Id).ShowNotificationAsync(NotifyMessageType.Error,
                        "Insomnia", $"Wechseln der {(targetConsole ? "Konsolen" : "Remote")}-Sitzung fehlgeschlagen", timeout: 20000);
                }
            }
        }

        void IDisposable.Dispose()
        {
            if (_overrideSwitch != null)
                _overrideSwitch.SwitchStateChanged -= ManualOverrideSwitch_StateChanged;

            _sessionManager.UserLogout -= SessionManager_SessionChanged;
            _sessionManager.ConsoleSessionChanged -= SessionManager_SessionChanged;
            _sessionManager.UserLogin -= SessionManager_SessionChanged;

            //_sessionService.ServiceStopped -= SessionService_Stopped;
            _sessionService.ServiceStarted -= SessionService_Started;
        }

        private void SessionService_Started(object sender, SessionEventArgs args)
        {
            Logger.LogDebug($"Bootstrapping NotificationArea[{args.Session.Id}]");

            UpdateNotifyArea(args.Session);
        }
        private void SessionManager_SessionChanged(object sender, SessionEventArgs args)
        {
            if (args is SessionLoginEventArgs)
                UpdateNotifyArea(args.Session);

            UpdateNotifyArea(_sessionManager, args.Session, null);
        }
        private void SessionManager_SessionImpersonationChanged(object sender, SessionEventArgs args)
        {
            UpdateNotifyArea(args.Session);
        }
        private void NetworkCommander_NetworkAvailabilityChanged(object sender, NetworkEventArgs args)
        {
            bool avail = args.Network.IsAvailable;
            bool moon = args.Network.Connection == Network.NetworkConnection.Moonrise;

            if (!avail)
                foreach (var svRef in _sessionService)
                {
                    svRef.Service.ShowNotificationAsync(NotifyMessageType.Warning, "Insomnia", $"Verbindung zu Netzwerk unterbrochen.").Wait();

                    Thread.Sleep(500);
                }

            UpdateNotifyArea(args.Network);

            if (avail)
                foreach (var svRef in _sessionService)
                {
                    if (moon)
                        svRef.Service.ShowNotificationAsync(NotifyMessageType.None, "Insomnia", $"Verbindung zu Netzwerk wiederhergestellt (via Moonrise).").Wait();
                    else
                        svRef.Service.ShowNotificationAsync(NotifyMessageType.None, "Insomnia", $"Verbindung zu Netzwerk wiederhergestellt.").Wait();
                }
        }
        private void NetworkCommander_NetworkTargetChanged(object sender, NetworkTargetEventArgs args)
        {
            UpdateNotifyArea(args.Network, args.Target);
        }
        private void NetworkCommander_ConfigChanged(object sender, EventArgs args)
        {
            UpdateNotifyArea(sender as NetworkCommanderConfig);
        }

        private void ManualOverrideSwitch_StateChanged(object sender, EventArgs args)
        {
            UpdateNotifyArea(sender as ManualOverrideSwitch);
        }

        private void UpdateNotifyArea(ISession sessionTarget = null)
        {
            if (_configTray.SessionSwitch != null)
            {
                UpdateNotifyArea(_sessionManager, sessionTarget);
            }

            if (_commanderConfig != null)
            {
                foreach (var svRef in _sessionService)
                    if (sessionTarget == null || svRef.Session == sessionTarget)
                        svRef.Service.IsMoonriseCommanderEnabled = _configUI.MoonriseCommander != null;

                foreach (var network in _commander.Networks)
                {
                    if (!network.IsAvailable)
                        continue;

                    UpdateNotifyArea(network, sessionTarget);
                }
            }

            // Options
            if (_overrideSwitch != null)
                UpdateNotifyArea(_overrideSwitch, sessionTarget);
            if (_commanderConfig != null)
                UpdateNotifyArea(_commanderConfig, sessionTarget);
        }
        private void UpdateNotifyArea(ISessionManager manager, ISession sessionTarget = null)
        {
            foreach (ISession session in _sessionManager.Sessions)
                UpdateNotifyArea(_sessionManager, session, sessionTarget);
        }
        private void UpdateNotifyArea(ISessionManager manager, ISession session, ISession sessionTarget = null)
        {
            foreach (var svRef in _sessionService)
                if (sessionTarget == null || svRef.Session == sessionTarget)
                {
                    var userInfo = new UserInfo()
                    {
                        SID = session.Id,

                        IsConsoleConnected = session.IsConsoleConnected
                    };

                    if (manager.FindSessionByID(session.Id) != null) // alive?
                    {
                        userInfo.Name = session.UserName;

                        bool allowConnect = false
                            || _configTray.SessionSwitch.AllowAdministrator && svRef.Session.Security.IsPrincipalAdministrator
                            || _configTray.SessionSwitch.AllowUser && svRef.Session.Security.IsPrincipalUser
                            || _configTray.SessionSwitch.AllowSelf && svRef.Session == session;

                        if (allowConnect)
                        {
                            if (_configTray.SessionSwitch.AllowConsole)
                                userInfo.AllowConnectToConsole = true;

                            if (_configTray.SessionSwitch.AllowRemote && svRef.Session.IsRemoteConnected)
                                userInfo.AllowConnectToRemote = false && userInfo.AllowConnectToConsole; // TODO maybe never?
                        }

                        svRef.Service.ShowAvailableConnectUser(userInfo).Wait();
                    }
                    else
                        svRef.Service.HideAvailableConnectUser(userInfo).Wait();
                }
        }
        private void UpdateNotifyArea(Network network, ISession sessionTarget = null)
        {
            foreach (var networkTarget in network.Targets)
            {
                UpdateNotifyArea(network, networkTarget, sessionTarget);
            }
        }
        private void UpdateNotifyArea(Network network, NetworkTarget target, ISession sessionTarget = null)
        {
            foreach (var svRef in _sessionService)
                if (sessionTarget == null || svRef.Session == sessionTarget)
                {
                    NetworkType type = network.Connection switch
                    {
                        Network.NetworkConnection.Ethernet => NetworkType.Wired,
                        Network.NetworkConnection.WiFi => NetworkType.Wireless,
                        Network.NetworkConnection.Moonrise => NetworkType.Remote,
                        _ => NetworkType.Unknown
                    };

                    var wt = new WakeTarget(target.Name, network.Name, type);

                    wt.AvailableModes = target.AvailableWakeModes.ToArray();

                    if (wt.AvailableModes.Length == 2
                        && wt.AvailableModes.Contains(WakeModeNone.ID)
                        && wt.AvailableModes.Contains(WakeModeWOL.ID))
                    {
                        wt.AvailableModes = new object[] { true, false };
                        wt.SelectedMode = target.WakeMode == WakeModeWOL.ID;
                    }
                    else
                    {
                        wt.SelectedMode = target.WakeMode;
                    }

                    try
                    {
                        if (network.IsAvailable)
                            svRef.Service.ShowWakeTarget(wt).Wait();
                        else
                            svRef.Service.HideWakeTarget(wt).Wait();
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e, $"UpdateNotifyArea failed (SID={svRef.Session.Id})");
                    }
                }
        }

        private void UpdateNotifyArea(ManualOverrideSwitch overrideSwitch, ISession sessionTarget = null)
        {
            foreach (var svRef in _sessionService)
                if (sessionTarget == null || svRef.Session == sessionTarget)
                {
                    try
                    {
                        svRef.Service.ShowWakeOption(new WakeOption(WakeOption.SLEEPLESS, overrideSwitch.Enabled)).Wait();
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e, $"UpdateNotifyArea failed (SID={svRef.Session.Id})");
                    }
                }
        }

        private void UpdateNotifyArea(NetworkCommanderConfig config, ISession sessionTarget = null)
        {
            foreach (var svRef in _sessionService)
                if (sessionTarget == null || svRef.Session == sessionTarget)
                {
                    try
                    {
                        svRef.Service.ShowWakeOption(new WakeOption(WakeOption.RESOLVE_IP, config.ResolveIPAddress)).Wait();
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e, $"UpdateNotifyArea failed (SID={svRef.Session.Id})");
                    }
                }
        }
    }
}