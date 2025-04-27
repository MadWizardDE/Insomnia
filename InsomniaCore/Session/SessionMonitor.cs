using Autofac;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Session.Manager;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace MadWizard.Insomnia.Session
{
    public class SessionMonitor(SessionMonitorConfig config, ISessionManager manager) : ResourceMonitor<SessionWatch>, IStartable
    {
        public required ILogger<SessionMonitor> Logger { get; set; }

        public void Start()
        {
            foreach (ISession session in manager)
                MayBeTrackSession(session);

            manager.UserLogon += SessionManager_UserLogin;
            manager.UserLogoff += SessionManager_UserLogout;

            Logger.LogDebug("Startup complete");
        }

        public void StopTrackingOf(uint sessionID)
        {
            foreach (var watch in this)
                if (watch?.Session.Id == sessionID)
                {
                    this.StopTracking(watch);

                    Logger.LogInformation($"Stopped tracking for ID={watch.Session.Id}");
                }
        }

        private void SessionManager_UserLogin(object? sender, ISession session)
        {
            MayBeTrackSession(session, true);
        }
        private void SessionManager_UserLogout(object? sender, ISession session)
        {
            foreach (var watch in this)
                if (watch?.Session == session)
                    this.StopTracking(watch);
        }

        private void MayBeTrackSession(ISession session, bool logon = false)
        {
            if (config.IgnoreClientName != null)
                if (config.IgnoreClientName == session.ClientName)
                    return;

            var watch = new SessionWatch(session);

            config.Configure(session, watch.ApplyConfiguration);

            if (watch.ShouldBeTracked)
            {
                if (this.StartTracking(watch) && logon)
                {
                    watch.TriggerLogon();
                }
            }
        }
    }
}
