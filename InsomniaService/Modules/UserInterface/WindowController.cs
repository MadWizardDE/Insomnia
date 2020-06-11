using Autofac.Features.OwnedInstances;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Lifetime;
using MadWizard.Insomnia.Service.Sessions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;
using System.Timers;

using static MadWizard.Insomnia.Configuration.UserInterfaceConfig;

namespace MadWizard.Insomnia.Service.UI
{
    class WindowController : ISessionChangeHandler
    {
        WindowCleanerConfig _config;

        Func<Owned<ISessionService<IWindowManagerService>>> _WindowManagerFactory;

        public WindowController(InsomniaConfig config, Func<Owned<ISessionService<IWindowManagerService>>> WindowManagerFactory)
        {
            _config = config.UserInterface.WindowCleaner;

            _WindowManagerFactory = WindowManagerFactory;
        }

        void ISessionChangeHandler.OnSessionChange(SessionChangeDescription desc)
        {
            void WipeTimer_Elapsed(object sender, ElapsedEventArgs e)
            {
                using var manager = _WindowManagerFactory();

                foreach (var pattern in _config.TitlePattern.Values)
                    manager.Value.SelectSession(desc.SessionId).Wipe(pattern.Text);
            }

            switch (desc.Reason)
            {
                case SessionChangeReason.RemoteConnect:
                case SessionChangeReason.ConsoleConnect:
                case SessionChangeReason.SessionUnlock:
                    if (_config.TitlePattern.Count > 0)
                    {
                        Timer wipeTimer = new Timer();
                        wipeTimer.Interval = _config.WaitTime;
                        wipeTimer.AutoReset = false;
                        wipeTimer.Elapsed += WipeTimer_Elapsed;
                        wipeTimer.Start();
                    }
                    break;
            }
        }
    }
}