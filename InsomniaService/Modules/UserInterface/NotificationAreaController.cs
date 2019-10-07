using Autofac;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Sessions;
using MadWizard.Insomnia.Service.SleepWatch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using static MadWizard.Insomnia.Configuration.SleepWatchConfig;
using static MadWizard.Insomnia.Service.Sessions.INotificationAreaService;
using static MadWizard.Insomnia.Service.SleepWatch.NetworkCommander;

namespace MadWizard.Insomnia.Service.UI
{
    class NotificationAreaController : IStartable, ISessionMessageHandler<ConfigureWakeOnLANMessage>, ISessionMessageHandler<ConfigureWakeOptionMessage>
    {
        static readonly string[] WAKE_MODES_DEFAULT = { WakeModeNone.ID, WakeModeWOL.ID };

        static readonly object[] WAKE_OPTIONS_CHECKED = { true, false };

        UserInferfaceConfig _configUI;
        UserInferfaceConfig.TrayMenuConfig _configTray;

        NetworkCommander _commander;
        NetworkCommanderConfig _commanderConfig;

        ISessionService<INotificationAreaService> _sessionService;

        public NotificationAreaController(InsomniaConfig config, Lazy<NetworkCommander> commander, Lazy<ISessionService<INotificationAreaService>> sessionService)
        {
            _configUI = config.UserInterface;
            _configTray = _configUI?.TrayMenu;

            if (_configTray != null)
            {
                _commander = commander.Value;
                _commander.NetworkAvailabilityChanged += NetworkCommander_NetworkAvailabilityChanged;
                _commander.NetworkTargetChanged += NetworkCommander_NetworkTargetChanged;

                _sessionService = sessionService.Value;
            }

            _commanderConfig = config.SleepWatch?.NetworkCommander;
            if (_commanderConfig != null)
                _commanderConfig.ConfigChanged += NetworkCommander_ConfigChanged;
        }

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
            throw new NotImplementedException();
        }

        private void NetworkCommander_NetworkAvailabilityChanged(object sender, NetworkEventArgs args)
        {
            bool avail = args.Network.IsAvailable;
            bool moon = args.Network.Connection == Network.NetworkConnection.Moonrise;

            if (!avail)
                foreach (INotificationAreaService service in _sessionService)
                {
                    service.ShowNotificationAsync(NotifyMessageType.Warning, "Insomnia", $"Verbindung zu Netzwerk unterbrochen.").Wait();

                    Thread.Sleep(500);
                }

            UpdateNotifyArea(args.Network);

            if (avail)
                foreach (INotificationAreaService service in _sessionService)
                {
                    if (moon)
                        service.ShowNotificationAsync(NotifyMessageType.None, "Insomnia", $"Verbindung zu Netzwerk wiederhergestellt (via Moonrise).").Wait();
                    else
                        service.ShowNotificationAsync(NotifyMessageType.None, "Insomnia", $"Verbindung zu Netzwerk wiederhergestellt.").Wait();
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
            if (_commanderConfig != null)
            {
                foreach (INotificationAreaService service in _sessionService)
                    service.IsMoonriseCommanderEnabled = _configUI.MoonriseCommander != null;

                foreach (var network in _commander.Networks)
                {
                    if (!network.IsAvailable)
                        continue;

                    UpdateNotifyArea(network);
                }

                UpdateNotifyArea(_commanderConfig);
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
            foreach (INotificationAreaService service in _sessionService)
            {
                var wt = new WakeTarget(target.Name, network.Name);

                wt.AvailableModes = target.AvailableWakeModes.ToArray();
                if (wt.AvailableModes.Equals(WAKE_MODES_DEFAULT))
                {
                    wt.AvailableModes = WAKE_OPTIONS_CHECKED;
                    wt.SelectedMode = target.WakeMode == WakeModeWOL.ID;
                }
                else
                {
                    wt.SelectedMode = target.WakeMode;
                }

                if (network.IsAvailable)
                    service.ShowWakeTarget(wt);
                else
                    service.HideWakeTarget(wt);
            }
        }
        private void UpdateNotifyArea(NetworkCommanderConfig config)
        {
            foreach (INotificationAreaService service in _sessionService)
            {
                service.ShowWakeOption(new WakeOption(WakeOption.RESOLVE_IP, config.ResolveIPAddress));
            }
        }
    }
}