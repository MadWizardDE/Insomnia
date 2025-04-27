using Autofac;
using MadWizard.Insomnia.Network;
using MadWizard.Insomnia.Service.Duo.Configuration;
using MadWizard.Insomnia.Service.Duo.Manager;
using MadWizard.Insomnia.Service.Duo.Sunshine;
using MadWizard.Insomnia.Session;
using MadWizard.Insomnia.Session.Manager;
using Microsoft.Extensions.Logging;
using System.ServiceProcess;
using System.Threading;
using System.Timers;

namespace MadWizard.Insomnia.Service.Duo
{
    internal class DuoStreamMonitor(DuoConfig config, DuoManager manager, ISessionManager sessionManager, IEnumerable<NetworkMonitor> networkMonitors) : ResourceMonitor<DuoInstance>, IStartable
    {
        public required ILogger<DuoStreamMonitor> Logger { get; set; }

        public required IComponentContext Context { get; set; }

        private SessionMonitor? SessionMonitor => Context.TryResolve<SessionMonitor>(out SessionMonitor? sessionMonitor) ? sessionMonitor : null;

        private bool IsFallbackMode => (config.DuoStreamMonitor?.UseFallback ?? false) || (!networkMonitors.Any());

        void IStartable.Start()
        {
            if (config.DuoStreamMonitor != null)
            {
                if (SessionMonitor != null)
                    SessionMonitor.Filters += SessionMonitor_Filter;

                sessionManager.UserLogon += SessionManager_UserLogon;
                sessionManager.UserLogoff += SessionManager_UserLogoff;

                manager.Started += DuoService_Started;
                manager.Stopped += DuoService_Stopped;

                Logger.LogInformation($"Monitor is enabled. Waiting for service to start...");
            }
        }

        private void DuoService_Started(object? sender, EventArgs e)
        {
            Logger.LogInformation($"Service is running:");

            foreach (var instance in manager)
            {
                instance.Started += DuoInstance_Started;
                instance.Stopped += DuoInstance_Stopped;

                SunshineService sunshine;

                if (IsFallbackMode)
                {
                    Logger.LogInformation($"Monitoring {instance}:{instance.Port} -> Fallback");

                    instance.StartTracking(sunshine = new SunshineFallbackService(instance.Port));
                }
                else
                {
                    Logger.LogInformation($"Monitoring {instance}:{instance.Port}");

                    instance.StartTracking(sunshine = new SunshineService(instance.Port));

                    foreach (var network in networkMonitors)
                    {
                        network.LocalHost.StartTracking(sunshine);
                    }
                }

                // prepare Instance, if Insomnia was started after it
                if ((instance.Session = sessionManager.Where(instance.HasInitiated).FirstOrDefault()) != null)
                {
                    SessionMonitor?.StopTrackingOf(instance.Session.Id);
                }

                sunshine.Inspect(TimeSpan.Zero); // trigger WaitForClient(), if in Fallback-Mode and Sunshine is not running

                this.StartTracking(instance);
            }
        }

        private void DuoInstance_Started(Event eventObj)
        {
            var instance = (DuoInstance)eventObj.Source!;

            foreach (var service in instance)
                if (service is SunshineFallbackService fallback)
                    fallback.StopWaiting();

            /**
             * For older versions of Duo (< 1.5.2).
             */
            if (instance.SessionID != null)
            {
                SessionMonitor?.StopTrackingOf((uint)instance.SessionID);
            }
        }

        private void DuoInstance_Stopped(Event eventObj)
        {
            var instance = (DuoInstance)eventObj.Source!;

            foreach (var service in instance)
                if (service is SunshineFallbackService fallback)
                    fallback.WaitForClient();
        }

        private void DuoService_Stopped(object? sender, EventArgs e)
        {
            Logger.LogInformation($"Service has stopped. Monitoring will be suspended...");

            foreach (var instance in this)
            {
                foreach (var service in instance)
                {
                    foreach (var network in networkMonitors)
                        network.LocalHost.StopTracking(service);

                    instance.StopTracking(service);

                    service.Dispose();
                }

                this.StopTracking(instance);
            }
        }

        #region Session Monitoring
        /**
         * Prevent monitoring of sessions that are already being monitored by DuoStreamMonitor.
         */
        private bool SessionMonitor_Filter(SessionWatch watch)
        {
            if (this.Where(instance => instance.HasInitiated(watch.Session)).Any())
                return false;

            return true;
        }

        private void SessionManager_UserLogon(object? sender, ISession session)
        {
            if (this.Where(instance => instance.HasInitiated(session)).FirstOrDefault() is DuoInstance instance)
                instance.Session = session;
        }
        private void SessionManager_UserLogoff(object? sender, ISession session)
        {
            if (this.Where(instance => instance.Session == session).FirstOrDefault() is DuoInstance instance)
                instance.Session = null;
        }
        #endregion

        #region Instance Action Handlers
        [ActionHandler("start")]
        internal async void HandleActionStart(DuoInstance instance)
        {
            if (await instance.Semaphore.WaitAsync(0))
            {
                try
                {
                    if (instance.IsRunning == false)
                        await manager.Start(instance);
                }
                finally
                {
                    instance.Semaphore.Release();
                }
            }
        }

        [ActionHandler("stop")]
        internal async void HandleActionStop(DuoInstance instance)
        {
            if (await instance.Semaphore.WaitAsync(0))
            {
                try
                {
                    if (instance.IsRunning == true)
                        await manager.Stop(instance);
                }
                finally
                {
                    instance.Semaphore.Release();
                }
            }
        }
        #endregion
    }
}
