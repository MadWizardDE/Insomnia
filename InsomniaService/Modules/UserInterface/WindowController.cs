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
    class WindowController : IPowerEventHandler
    {
        WindowCleanerConfig _config;

        Func<Owned<ISessionService<IWindowManagerService>>> _WindowManagerFactory;

        public WindowController(InsomniaConfig config, Func<Owned<ISessionService<IWindowManagerService>>> WindowManagerFactory)
        {
            _config = config.UserInterface.WindowCleaner;

            _WindowManagerFactory = WindowManagerFactory;
        }

        void IPowerEventHandler.OnPowerEvent(PowerBroadcastStatus status)
        {
            void WipeTimer_Elapsed(object sender, ElapsedEventArgs e)
            {
                var manager = _WindowManagerFactory();

                foreach (var pattern in _config.TitlePattern.Values)
                    foreach (var service in manager.Value)
                        service.Service.Wipe(pattern.Text);

                manager.Dispose();
            }

            switch (status)
            {
                case PowerBroadcastStatus.ResumeSuspend:
                    if (_config.TitlePattern.Count > 0)
                    {
                        Timer wipeTimer = new Timer();
                        wipeTimer.Interval = 2000;
                        wipeTimer.AutoReset = false;
                        wipeTimer.Elapsed += WipeTimer_Elapsed;
                        wipeTimer.Start();
                    }
                    break;
            }
        }
    }
}