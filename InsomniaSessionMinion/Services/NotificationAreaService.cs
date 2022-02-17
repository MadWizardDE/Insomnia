using MadWizard.Insomnia.Minion.Properties;
using MadWizard.Insomnia.Service;
using MadWizard.Insomnia.Service.Sessions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using wyDay.Controls;
using static MadWizard.Insomnia.Service.Sessions.INotificationAreaService;

namespace MadWizard.Insomnia.Minion.Services
{
    class NotificationAreaService : INotificationAreaService, IDisposable
    {
        SessionMinionConfig _config;

        IUserInterface _userInterface;
        IUserMessenger _userMessenger;

        NotifyIcon _notifyIcon;
        //VistaMenu _vistaMenu;

        bool _moonriseCommander;
        IDictionary<int, UserInfo> _connectUsers;
        IDictionary<string, IDictionary<string, WakeTarget>> _wakeTargets;
        IDictionary<string, WakeOption> _wakeOptions;

        public NotificationAreaService(SessionMinionConfig config, IUserInterface ui, IUserMessenger messenger)
        {
            _config = config;

            _connectUsers = new ConcurrentDictionary<int, UserInfo>();
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
            get
            {
                bool availableTargets = _wakeTargets.Count > 0;
                bool sleepless = _wakeOptions.TryGetValue(WakeOption.SLEEPLESS, out WakeOption option) && (bool)option.Value;

                return availableTargets ? Resources.MoonWhiteOutline12
                        : sleepless ? Resources.MoonBlackRedEyeOutline24
                            : Resources.MoonBlackOutline24;
            }
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
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Icon = TrayIcon;

            if (_connectUsers.Count > 0 || _wakeTargets.Count > 0 || _moonriseCommander)
            {
                //_vistaMenu = new VistaMenu();

                _notifyIcon.ContextMenuStrip = new ContextMenuStrip();

                //if (_moonriseCommander)
                //{
                //    MenuItem commander = new MenuItem("Moonrise Commander");
                //    _vistaMenu.SetImage(commander, new Bitmap(Resources.Moonrise, new Size(16, 16)));
                //    commander.DefaultItem = true;
                //    commander.Click += ContextMenu_MoonriseCommanderClicked;
                //    _notifyIcon.ContextMenu.MenuItems.Add(commander);

                //    _notifyIcon.ContextMenu.MenuItems.Add("-"); // Seperator

                //    _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
                //}

                if (_connectUsers.Count > 0)
                {
                    if (_notifyIcon.ContextMenuStrip.Items.Count > 0)
                        //if (_notifyIcon.ContextMenu.MenuItems[_notifyIcon.ContextMenu.MenuItems.Count - 1].Text != "-")
                        _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator()); // Seperator hinzufügen (wenn nicht schon vorhanden)

                    ToolStripMenuItem consoleSessions = new ToolStripMenuItem("Konsolen-Sitzung")
                    {
                        Image = new Bitmap(Resources.Monitor64, new Size(16, 16))
                    };

                    //_vistaMenu.SetImage(consoleSessions, new Bitmap(Resources.Monitor64, new Size(16, 16)));

                    foreach (UserInfo user in _connectUsers.Values)
                    {
                        void ContextMenu_ConsoleSessionClicked(object sender, EventArgs args)
                        {
                            _userMessenger.SendMessage(new ConnectToConsoleMessage(user));
                        }

                        ToolStripMenuItem userItem = new ToolStripMenuItem(user.Name);
                        userItem.Enabled = user.AllowConnectToConsole;
                        userItem.Checked = user.IsConsoleConnected;
                        if (!user.IsConsoleConnected)
                            userItem.Click += ContextMenu_ConsoleSessionClicked;
                        consoleSessions.DropDownItems.Add(userItem);
                    }

                    _notifyIcon.ContextMenuStrip.Items.Add(consoleSessions);

                    if (_connectUsers.Values.Where(u => u.IsRemoteConnected).Count() > 0)
                    {
                        ToolStripMenuItem remoteSessions = new ToolStripMenuItem("Remote-Sitzung")
                        {
                            Image = new Bitmap(Resources.UserSide32, new Size(16, 16))
                        };

                        var sid = Process.GetCurrentProcess().SessionId;

                        foreach (UserInfo user in _connectUsers.Values.Where(u => u.IsRemoteConnected))
                        {
                            //void ContextMenu_RemoteSessionClicked(object sender, EventArgs args)
                            //{
                            //    _userMessenger.SendMessage(new ConnectToRemoteMessage(user));
                            //}

                            ToolStripMenuItem userItem = new ToolStripMenuItem(user.Name);
                            userItem.Checked = user.SID == sid;
                            userItem.Enabled = false;
                            //userItem.Click += ContextMenu_RemoteSessionClicked;
                            remoteSessions.DropDownItems.Add(userItem);
                        }

                        foreach (UserInfo user in _connectUsers.Values.Where(u => u.IsRemoteConnected && u.SID == sid))
                        {
                            void ContextMenu_DisconnectRemoteSessionClicked(object sender, EventArgs args)
                            {
                                _userMessenger.SendMessage(new DisconnectSessionMessage(user));
                            }

                            remoteSessions.DropDownItems.Add(new ToolStripSeparator());

                            ToolStripMenuItem disconnectItem = new ToolStripMenuItem("Trennen");
                            disconnectItem.Click += ContextMenu_DisconnectRemoteSessionClicked;
                            remoteSessions.DropDownItems.Add(disconnectItem);

                            _notifyIcon.ContextMenuStrip.Items.Insert(0, new ToolStripSeparator());

                            ToolStripMenuItem hostItem = new ToolStripMenuItem(System.Net.Dns.GetHostName());
                            hostItem.Enabled = false;
                            _notifyIcon.ContextMenuStrip.Items.Insert(0, hostItem);
                        }

                        _notifyIcon.ContextMenuStrip.Items.Add(remoteSessions);
                    }
                }

                if (_wakeTargets.Count > 0)
                {
                    if (_wakeTargets.TryGetValue("", out var defaultWakeGroup))
                        AddWakeGroup(defaultWakeGroup.Values);
                    foreach (var groupName in _wakeTargets.Keys.Where(name => name != ""))
                        AddWakeGroup(_wakeTargets[groupName].Values);
                }

                if (_wakeOptions.Count > 0)
                {
                    _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator()); // Seperator

                    ToolStripMenuItem menuOptions = new ToolStripMenuItem("Optionen");

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
                                case WakeOption.SLEEPLESS:
                                    return "Schlaflos";
                                default:
                                    return option.Key;
                            }
                        }

                        ToolStripMenuItem optionItem = new ToolStripMenuItem(ToLabel(option));
                        if (option.Value is bool check)
                        {
                            optionItem.Checked = check;
                            optionItem.Click += ContextMenu_OptionClicked;
                            optionItem.Enabled = option.Authorized;
                        }
                        else
                            optionItem.Enabled = false;

                        menuOptions.DropDownItems.Add(optionItem);
                    }

                    _notifyIcon.ContextMenuStrip.Items.Add(menuOptions);
                }

                if (_notifyIcon.ContextMenuStrip.Items[_notifyIcon.ContextMenuStrip.Items.Count - 1] is ToolStripSeparator)
                    _notifyIcon.ContextMenuStrip.Items.RemoveAt(_notifyIcon.ContextMenuStrip.Items.Count - 1);

                void AddWakeGroup(IEnumerable<WakeTarget> targets)
                {
                    if (_notifyIcon.ContextMenuStrip.Items.Count > 0)
                        if (!(_notifyIcon.ContextMenuStrip.Items[_notifyIcon.ContextMenuStrip.Items.Count - 1] is ToolStripSeparator))
                            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator()); // Seperator hinzufügen (wenn nicht schon vorhanden)

                    NetworkType networkType = targets.Select(t => t.NetworkType).Distinct().Single();
                    string networkName = targets.Select(t => t.NetworkName).Distinct().Single();

                    if (networkName != "")
                    {
                        Bitmap networkIcon = networkType switch
                        {
                            NetworkType.Wired => Resources.Wired,
                            NetworkType.Wireless => Resources.WiFi,
                            NetworkType.Remote => Resources.Moonrise,
                            _ => Resources.Unknown
                        };

                        ToolStripMenuItem header = new ToolStripMenuItem(networkName)
                        {
                            Enabled = false,
                            Image = new Bitmap(networkIcon, new Size(16, 16))
                        };


                        _notifyIcon.ContextMenuStrip.Items.Add(header);
                    }

                    foreach (WakeTarget target in targets)
                    {
                        void ContextMenu_TargetClicked(object sender, EventArgs args)
                        {
                            WakeTarget target = (WakeTarget)(sender as ToolStripMenuItem).Tag;

                            _userMessenger.SendMessage(new ConfigureWakeOnLANMessage(new WakeTarget { Name = target.Name, NetworkName = target.NetworkName, SelectedMode = !(bool)target.SelectedMode }));
                        }
                        void ContextMenu_ModeClicked(object sender, EventArgs args)
                        {
                            string mode = (sender as ToolStripMenuItem).Tag as string;

                            if ((string)target.SelectedMode != mode)
                                _userMessenger.SendMessage(new ConfigureWakeOnLANMessage(new WakeTarget { Name = target.Name, SelectedMode = mode }));
                        }

                        ToolStripItem item;
                        if (target.SelectedMode is bool enabled)
                        {
                            ToolStripMenuItem button = new ToolStripMenuItem(target.Name);

                            button.Tag = target;
                            button.Checked = enabled;
                            button.Enabled = target.AvailableModes.Contains(!enabled);
                            button.Click += ContextMenu_TargetClicked;
                            item = button;
                        }
                        else if (target.SelectedMode is string selectedMode)
                        {
                            ToolStripMenuItem menu = new ToolStripMenuItem(target.Name);

                            foreach (string mode in target.AvailableModes)
                            {
                                ToolStripMenuItem itemOption = new ToolStripMenuItem(mode.ToUpper()); // TODO Name
                                itemOption.Tag = mode;
                                itemOption.Checked = mode == selectedMode;
                                itemOption.Click += ContextMenu_ModeClicked;

                                // TODO Icon

                                menu.DropDownItems.Add(itemOption);
                            }

                            item = menu;
                        }
                        else
                            throw new ArgumentException($"Unrecognized Option = {target.SelectedMode}");

                        _notifyIcon.ContextMenuStrip.Items.Add(item);
                    }
                }
            }
        }
        private void DestroyTrayIcon()
        {
            //_vistaMenu?.Dispose();
            //_vistaMenu = null;

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

        public async Task ShowAvailableConnectUser(UserInfo user)
        {
            _connectUsers[user.SID] = user;

            await _userInterface.SendActionAsync(UpdateContextMenu);
        }
        public async Task HideAvailableConnectUser(UserInfo user)
        {
            _connectUsers.Remove(user.SID);

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