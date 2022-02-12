using Autofac.Features.OwnedInstances;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Lifetime;
using MadWizard.Insomnia.Service.Sessions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

using static MadWizard.Insomnia.Configuration.UserInterfaceConfig;

namespace MadWizard.Insomnia.Service.UI
{
    class WindowController : ISessionChangeHandler
    {
        WindowManagerConfig _config;

        Func<Owned<ISessionService<IWindowManagerService>>> _WindowManagerFactory;

        Timer _manageTimer;
        ISet<int> _manageSessions;

        public WindowController(InsomniaConfig config, Func<Owned<ISessionService<IWindowManagerService>>> WindowManagerFactory)
        {
            _config = config.UserInterface.WindowManager;

            _WindowManagerFactory = WindowManagerFactory;

            _manageSessions = new HashSet<int>();
        }

        [Autowired]
        ILogger<WindowController> Logger { get; set; }

        async void ManageTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            using var manager = _WindowManagerFactory();

            foreach (int sessionId in _manageSessions)
            {
                foreach (var closeConfig in _config.CloseWindow.Values)
                {
                    await manager.Value.SelectSession(sessionId).CloseWindow(closeConfig.Title);
                }

                foreach (var restartConfig in _config.RestartProcess.Values)
                {
                    string processName = restartConfig.Name;
                    TimeSpan after = TimeSpan.FromMilliseconds(restartConfig.After);
                    TimeSpan timeout = TimeSpan.FromMilliseconds(restartConfig.Timeout);

                    try
                    {
                        await manager.Value.SelectSession(sessionId).TerminateProcess(processName, after, timeout, true);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"{ex}");
                    }
                }

                _manageSessions.Remove(sessionId);
            }

            _manageTimer = null;
        }

        void ISessionChangeHandler.OnSessionChange(SessionChangeDescription desc)
        {
            switch (desc.Reason)
            {
                case SessionChangeReason.RemoteConnect:
                case SessionChangeReason.ConsoleConnect:
                case SessionChangeReason.SessionUnlock:

                    lock (_manageSessions)
                    {
                        if (_manageTimer == null)
                        {
                            _manageTimer = new Timer();
                            _manageTimer.Interval = _config.WaitTime;
                            _manageTimer.AutoReset = false;
                            _manageTimer.Elapsed += ManageTimer_Elapsed;
                            _manageTimer.Start();
                        }

                        _manageSessions.Add(desc.SessionId);
                    }

                break;
            }
        }
    }
}