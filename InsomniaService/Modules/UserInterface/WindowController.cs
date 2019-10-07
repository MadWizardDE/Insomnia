using Autofac.Features.OwnedInstances;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Lifetime;
using MadWizard.Insomnia.Service.Sessions;
using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;
using System.Timers;

using static MadWizard.Insomnia.Configuration.UserInferfaceConfig;

namespace MadWizard.Insomnia.Service.UI
{
    class WindowController : IPowerEventHandler
    {
        WindowCleanerConfig _config;

        Func<Owned<ISessionService<IWindowCleanerService>>> _WindowCleanerFactory;

        public WindowController(InsomniaConfig config, Func<Owned<ISessionService<IWindowCleanerService>>> WindowCleanerFactory)
        {
            _config = config.UserInterface?.WindowCleaner;

            _WindowCleanerFactory = WindowCleanerFactory;
        }

        void IPowerEventHandler.OnPowerEvent(PowerBroadcastStatus status)
        {
            void WipeTimer_Elapsed(object sender, ElapsedEventArgs e)
            {
                var cleaner = _WindowCleanerFactory();

                foreach (var pattern in _config.TitlePattern.Values)
                    foreach (var service in cleaner.Value)
                        service.Service.Wipe(pattern.Text);

                cleaner.Dispose();
            }

            switch (status)
            {
                case PowerBroadcastStatus.ResumeSuspend:
                    if (_config != null && _config.TitlePattern.Count > 0)
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