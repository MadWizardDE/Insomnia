using MadWizard.Insomnia.Service.Sessions;
using MadWizard.Insomnia.Minion.Properties;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using wyDay.Controls;

using static MadWizard.Insomnia.Service.Sessions.INotificationAreaService;
using Microsoft.Extensions.Logging;
using MadWizard.Insomnia.Service;

namespace MadWizard.Insomnia.Minion.Services
{
    class NotificationAreaService : INotificationAreaService, IDisposable
    {
        IUserInterface _userInterface;
        IUserMessenger _userMessenger;

        NotifyIcon _notifyIcon;
        VistaMenu _vistaMenu;

        bool _moonriseCommander;
        IDictionary<int, UserInfo> _consoleUsers;
        IDictionary<string, IDictionary<string, WakeTarget>> _wakeTargets;
        IDictionary<string, WakeOption> _wakeOptions;

        public NotificationAreaService(IUserInterface ui, IUserMessenger messenger)
        {
            _consoleUsers = new ConcurrentDictionary<int, UserInfo>();
            _wakeTargets = new ConcurrentDictionary<string, IDictionary<string, WakeTarget>>();
            _wakeOptions = new ConcurrentDictionary<string, WakeOption>();

            _userInterface = ui;
            _userInterface.SendAction(CreateTrayIcon);
            _userInterface.SendAction(UpdateContextMenu);

            _userMessenger = messenger;
        }

        [Autowired]
        ILogger<NotificationAreaService> Logger { get; set; }

        Icon TrayIcon
        {
            get => _wakeTargets.Count > 0 ? Resources.MoonWhiteOutline12 : Resources.MoonBlackOutline24;
        }

        #region Component Lifecycle
        private void CreateTrayIcon()
        {
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = "Insomnia";
            _notifyIcon.Icon = TrayIcon;
            _notifyIcon.Visible = true;
        }
        private void UpdateContextMenu()
        {
            static void NotifyIcon_DoubleClick(object sender, EventArgs args)
            {
                ContextMenu_MoonriseCommanderClicked(sender, args);
            }
            static void ContextMenu_MoonriseCommanderClicked(object sender, EventArgs args)
            {
                //new MoonriseWindow().ShowDialog();
                throw new NotImplementedException();
            }

            _notifyIcon.DoubleClick -= NotifyIcon_DoubleClick;
            _notifyIcon.ContextMenu?.Dispose();
            _notifyIcon.Icon = TrayIcon;

            if (_consoleUsers.Count > 0 || _wakeTargets.Count > 0 || _moonriseCommander)
            {
                _vistaMenu = new VistaMenu();

                _notifyIcon.ContextMenu = new ContextMenu();

                if (_moonriseCommander)
                {
                    MenuItem commander = new MenuItem("Moonrise Commander");
                    _vistaMenu.SetImage(commander, new Bitmap(Resources.Moonrise, new Size(16, 16)));
                    commander.DefaultItem = true;
                    commander.Click += ContextMenu_MoonriseCommanderClicked;
                    _notifyIcon.ContextMenu.MenuItems.Add(commander);

                    _notifyIcon.ContextMenu.MenuItems.Add("-"); // Seperator

                    _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
                }

                if (_consoleUsers.Count > 0)
                {
                    if (_notifyIcon.ContextMenu.MenuItems.Count > 0)
                        if (_notifyIcon.ContextMenu.MenuItems[_notifyIcon.ContextMenu.MenuItems.Count - 1].Name != "-")
                            _notifyIcon.ContextMenu.MenuItems.Add("-"); // Seperator hinzufügen (wenn nicht schon vorhanden)

                    MenuItem consoleSessions = new MenuItem("Konsolen-Sitzung");

                    _vistaMenu.SetImage(consoleSessions, new Bitmap(Resources.User16, new Size(16, 16)));

                    foreach (UserInfo user in _consoleUsers.Values)
                    {
                        void ContextMenu_ConsoleUserClicked(object sender, EventArgs args)
                        {
                            _userMessenger.SendMessage(new ConnectToConsoleMessage(user));
                        }

                        MenuItem userItem = new MenuItem(user.Name);
                        userItem.Checked = user.IsConsoleConnected;
                        if (!user.IsConsoleConnected)
                            userItem.Click += ContextMenu_ConsoleUserClicked;
                        consoleSessions.MenuItems.Add(userItem);
                    }

                    _notifyIcon.ContextMenu.MenuItems.Add(consoleSessions);
                }

                if (_wakeTargets.Count > 0)
                {
                    if (_wakeTargets.TryGetValue("", out var defaultWakeGroup))
                        AddWakeGroup(defaultWakeGroup.Values);
                    foreach (var groupName in _wakeTargets.Keys.Where(name => name != ""))
                        AddWakeGroup(_wakeTargets[groupName].Values);

                    if (_wakeOptions.Count > 0)
                    {
                        _notifyIcon.ContextMenu.MenuItems.Add("-"); // Seperator

                        MenuItem menuOptions = new MenuItem("Optionen");

                        foreach (WakeOption option in _wakeOptions.Values)
                        {
                            void ContextMenu_OptionClicked(object sender, EventArgs args)
                            {
                                if (option.Value is bool check)
                                {
                                    _userMessenger.SendMessage(new ConfigureWakeOptionMessage(new WakeOption(option.Key, !check)));
                                }
                            }

                            static string ToLabel(WakeOption option)
                            {
                                switch (option.Key)
                                {
                                    case WakeOption.RESOLVE_IP:
                                        return "IP-Adresse auflösen";
                                    default:
                                        return option.Key;
                                }
                            }

                            MenuItem optionItem = new MenuItem(ToLabel(option));
                            if (option.Value is bool check)
                            {
                                optionItem.Checked = check;
                                optionItem.Click += ContextMenu_OptionClicked;
                            }
                            else
                                optionItem.Enabled = false;

                            menuOptions.MenuItems.Add(optionItem);
                        }

                        _notifyIcon.ContextMenu.MenuItems.Add(menuOptions);
                    }
                }

                ((System.ComponentModel.ISupportInitialize)(_vistaMenu)).EndInit();

                void AddWakeGroup(IEnumerable<WakeTarget> targets)
                {
                    if (_notifyIcon.ContextMenu.MenuItems.Count > 0)
                        if (_notifyIcon.ContextMenu.MenuItems[_notifyIcon.ContextMenu.MenuItems.Count - 1].Name != "-")
                            _notifyIcon.ContextMenu.MenuItems.Add("-"); // Seperator hinzufügen (wenn nicht schon vorhanden)

                    NetworkType networkType = targets.Select(t => t.NetworkType).Distinct().Single();
                    string networkName = targets.Select(t => t.NetworkName).Distinct().Single();

                    if (networkName != "")
                    {
                        MenuItem header = new MenuItem(networkName)
                        {
                            Enabled = false
                        };

                        Bitmap networkIcon = networkType switch
                        {
                            NetworkType.Wired => Resources.Wired,
                            NetworkType.Wireless => Resources.WiFi,
                            NetworkType.Remote => Resources.Moonrise,
                            _ => Resources.Unknown
                        };

                        _vistaMenu.SetImage(header, new Bitmap(networkIcon, new Size(16, 16)));

                        _notifyIcon.ContextMenu.MenuItems.Add(header);
                    }

                    foreach (WakeTarget target in targets)
                    {
                        void ContextMenu_TargetClicked(object sender, EventArgs args)
                        {
                            WakeTarget target = (WakeTarget)(sender as MenuItem).Tag;

                            _userMessenger.SendMessage(new ConfigureWakeOnLANMessage(new WakeTarget { Name = target.Name, NetworkName = target.NetworkName, SelectedMode = !(bool)target.SelectedMode }));
                        }
                        void ContextMenu_ModeClicked(object sender, EventArgs args)
                        {
                            string mode = (sender as MenuItem).Tag as string;

                            if ((string)target.SelectedMode != mode)
                                _userMessenger.SendMessage(new ConfigureWakeOnLANMessage(new WakeTarget { Name = target.Name, SelectedMode = mode }));
                        }

                        MenuItem item = new MenuItem(target.Name);

                        if (target.SelectedMode is bool enabled)
                        {
                            item.Tag = target;
                            item.Checked = enabled;
                            item.Enabled = target.AvailableModes.Contains(!enabled);
                            item.Click += ContextMenu_TargetClicked;
                        }
                        else if (target.SelectedMode is string selectedMode)
                        {
                            foreach (string mode in target.AvailableModes)
                            {
                                MenuItem itemOption = new MenuItem(mode.ToUpper()); // TODO Name
                                itemOption.Tag = mode;
                                itemOption.Checked = mode == selectedMode;
                                itemOption.Click += ContextMenu_ModeClicked;

                                // TODO Icon

                                item.MenuItems.Add(itemOption);
                            }
                        }
                        else
                            throw new ArgumentException($"Unrecognized Option = {target.SelectedMode}");

                        _notifyIcon.ContextMenu.MenuItems.Add(item);
                    }
                }
            }
        }
        private void DestroyTrayIcon()
        {
            _vistaMenu?.Dispose();
            _vistaMenu = null;

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
        #endregion

        #region INotificationAreaService
        public bool IsMoonriseCommanderEnabled
        {
            get => _moonriseCommander;

            set
            {
                if (_moonriseCommander != value)
                {
                    _moonriseCommander = value;

                    _userInterface.SendAction(UpdateContextMenu);
                }
            }
        }

        public async Task ShowNotificationAsync(NotifyMessageType type, string title, string text, int timeout)
        {
            Logger.LogInformation($"Notification[{type}] = {title} | {text}");

            await _userInterface.SendActionAsync(() =>
            {
                _notifyIcon.ShowBalloonTip(timeout, title, text, (ToolTipIcon)type);
            });
        }

        public async Task ShowAvailableConsoleUser(UserInfo user)
        {
            _consoleUsers[user.SID] = user;

            await _userInterface.SendActionAsync(UpdateContextMenu);
        }
        public async Task HideAvailableConsoleUser(UserInfo user)
        {
            _consoleUsers.Remove(user.SID);

            await _userInterface.SendActionAsync(UpdateContextMenu);
        }

        public async Task ShowWakeTarget(WakeTarget target)
        {
            if (!_wakeTargets.TryGetValue(target.NetworkName, out var networkTargets))
                _wakeTargets[target.NetworkName] = networkTargets = new Dictionary<string, WakeTarget>();

            networkTargets[target.Name] = target;

            await _userInterface.SendActionAsync(UpdateContextMenu);
        }
        public async Task HideWakeTarget(WakeTarget target)
        {
            if (_wakeTargets.TryGetValue(target.NetworkName, out var networkTargets))
            {
                networkTargets.Remove(target.Name);

                if (networkTargets.Count == 0)
                    _wakeTargets.Remove(target.NetworkName);

                await _userInterface.SendActionAsync(UpdateContextMenu);
            }
        }

        public async Task ShowWakeOption(WakeOption option)
        {
            _wakeOptions[option.Key] = option;

            await _userInterface.SendActionAsync(UpdateContextMenu);
        }
        public async Task HideWakeOption(WakeOption option)
        {
            _wakeOptions.Remove(option.Key);

            await _userInterface.SendActionAsync(UpdateContextMenu);
        }

        public async Task Recreate()
        {
            await _userInterface.SendActionAsync(DestroyTrayIcon);
            await _userInterface.SendActionAsync(CreateTrayIcon);
            await _userInterface.SendActionAsync(UpdateContextMenu);
        }
        #endregion

        void IDisposable.Dispose()
        {
            _userInterface.SendAction(DestroyTrayIcon);
        }
    }
}