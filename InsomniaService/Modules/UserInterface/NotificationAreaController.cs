using Autofac;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Lifetime;
using MadWizard.Insomnia.Service.Sessions;
using MadWizard.Insomnia.Service.SleepWatch;
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
using static MadWizard.Insomnia.Service.Sessions.INotificationAreaService;
using static MadWizard.Insomnia.Service.SleepWatch.NetworkCommander;

namespace MadWizard.Insomnia.Service.UI
{
    class NotificationAreaController : IStartable, ISessionChangeHandler,
        ISessionMessageHandler<ConfigureWakeOnLANMessage>,
        ISessionMessageHandler<ConfigureWakeOptionMessage>,
        ISessionMessageHandler<ConnectToConsoleMessage>,
        IDisposable

    {
        UserInterfaceConfig _configUI;
        UserInterfaceConfig.TrayMenuConfig _configTray;

        NetworkCommander _commander;
        NetworkCommanderConfig _commanderConfig;

        ISessionManager _sessionManager;
        ISessionService<INotificationAreaService> _sessionService;

        public NotificationAreaController(InsomniaConfig config,
            ISessionManager sessionManager, Lazy<ISessionService<INotificationAreaService>> sessionService,
            NetworkCommander commander = null)
        {
            _configUI = config.UserInterface;
            _configTray = _configUI?.TrayMenu;

            _sessionManager = sessionManager;

            if (_configTray != null)
            {
                _sessionService = sessionService.Value;

                if (_configTray.ShowConsoleSwitch)
                {
                    _sessionManager.UserLogin += SessionManager_SessionChanged;
                    _sessionManager.ConsoleSessionChanged += SessionManager_SessionChanged;
                    _sessionManager.UserLogout += SessionManager_SessionChanged;
                }
            }

            _commanderConfig = config.SleepWatch?.NetworkCommander;

            if (_commanderConfig != null && commander != null)
            {
                _commander = commander;
                _commander.NetworkAvailabilityChanged += NetworkCommander_NetworkAvailabilityChanged;
                _commander.NetworkTargetChanged += NetworkCommander_NetworkTargetChanged;
                _commanderConfig.ConfigChanged += NetworkCommander_ConfigChanged;
            }
        }
        [Autowired]
        ILogger<NotificationAreaController> Logger { get; set; }

        void IStartable.Start()
        {
            UpdateNotifyArea();
        }

        void ISessionChangeHandler.OnSessionChange(SessionChangeDescription desc)
        {
            switch (desc.Reason)
            {
                case SessionChangeReason.SessionUnlock:
                case SessionChangeReason.RemoteConnect:
                case SessionChangeReason.ConsoleConnect:
                    //_sessionService.SelectSession(desc.SessionId).Recreate();
                    break;

                default: break;
            }
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
            }
        }
        async void ISessionMessageHandler<ConnectToConsoleMessage>.Handle(ISession session, ConnectToConsoleMessage message)
        {
            // TODO asynchrone verarbeitung?

            ISession source, target = _sessionManager.ConsoleSession;
            if (message.User.SID > 0)
                source = _sessionManager.FindSessionByID(message.User.SID);
            else
                source = _sessionManager.FindSessionByUserName(message.User.Name);

            if (source != target)
            {
                Logger.LogInformation($"Connecting SID={source.Id} to SID={(target != null ? target.Id.ToString() : "Console")}...");

                try
                {
                    _sessionManager.ConnectSession(source, target);

                    await Task.Delay(5000);

                    foreach (var svcRef in _sessionService)
                        if (target != null)
                            await svcRef.Service.ShowNotificationAsync(NotifyMessageType.Info,
                                "Insomnia", $"Die Konsolen-Sitzung wurde erfolgreich von '{target.UserName}' zu '{source.UserName}' gewechselt.", timeout: 20000);
                        else
                            await svcRef.Service.ShowNotificationAsync(NotifyMessageType.Info,
                                "Insomnia", $"Die Konsolen-Sitzung wurde erfolgreich zu '{source.UserName}' gewechselt.", timeout: 20000);

                }
                catch (Exception e)
                {
                    Logger.LogError(e, "ConnectToConsole() failed");
                }
            }
        }

        void IDisposable.Dispose()
        {
            _sessionManager.UserLogout -= SessionManager_SessionChanged;
            _sessionManager.ConsoleSessionChanged -= SessionManager_SessionChanged;
            _sessionManager.UserLogin -= SessionManager_SessionChanged;
        }

        private void SessionManager_SessionChanged(object sender, UserEventArgs e)
        {
            UpdateNotifyArea(e.Session);
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

        private void UpdateNotifyArea()
        {
            if (_configTray.ShowConsoleSwitch)
            {
                foreach (ISession session in _sessionManager.Sessions)
                    UpdateNotifyArea(session);
            }

            if (_commanderConfig != null)
            {
                foreach (var svRef in _sessionService)
                    svRef.Service.IsMoonriseCommanderEnabled = _configUI.MoonriseCommander != null;

                foreach (var network in _commander.Networks)
                {
                    if (!network.IsAvailable)
                        continue;

                    UpdateNotifyArea(network);
                }

                UpdateNotifyArea(_commanderConfig);
            }
        }
        private void UpdateNotifyArea(ISession session)
        {
            foreach (var svRef in _sessionService)
            {
                var userInfo = new UserInfo()
                {
                    SID = session.Id,
                    Name = session.UserName,
                    IsConsoleConnected = session.IsConsoleConnected
                };


                if (_sessionManager.FindSessionByID(session.Id) != null)
                    svRef.Service.ShowAvailableConsoleUser(userInfo).Wait();
                else
                    svRef.Service.HideAvailableConsoleUser(userInfo).Wait();
            }
        }
        private void UpdateNotifyArea(Network network)
        {
            foreach (var networkTarget in network.Targets)
            {
                UpdateNotifyArea(network, networkTarget);
            }
        }
        private void UpdateNotifyArea(Network network, NetworkTarget target)
        {
            foreach (var svRef in _sessionService)
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
        private void UpdateNotifyArea(NetworkCommanderConfig config)
        {
            foreach (var svRef in _sessionService)
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