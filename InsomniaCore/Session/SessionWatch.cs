using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Session.Manager;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace MadWizard.Insomnia.Session
{
    public class SessionWatch(ISession session) : ResourceMonitor<SessionProcessGroup>
    {
        [EventContext]
        public ISession Session => session;

        private bool _applied = false;
        private bool _appliedAction = false;
        private bool _appliedProcess = false;

        internal bool ShouldBeTracked => _applied || _appliedAction || _appliedProcess;

        public event EventInvocation? Login;
        public event EventInvocation? RemoteLogin;
        public event EventInvocation? ConsoleLogin;

        internal void ApplyConfiguration(SessionWatchDescriptor desc)
        {
            if (desc.IgnoreClientName != null)
                if (desc.IgnoreClientName == session.ClientName)
                    return;

            this._applied = true;

            if (desc.OnIdle != null)
            {
                AddEventAction(nameof(Idle), desc.OnIdle);

                this._appliedAction = true;
            }

            if (desc.OnLogin != null)
            {
                AddEventAction(nameof(Login), desc.OnLogin);

                this._appliedAction = true;
            }

            if (desc.OnRemoteLogin != null)
            {
                AddEventAction(nameof(RemoteLogin), desc.OnRemoteLogin);

                this._appliedAction = true;
            }

            if (desc.OnConsolesLogin != null)
            {
                AddEventAction(nameof(ConsoleLogin), desc.OnConsolesLogin);

                this._appliedAction = true;
            }

            foreach (var info in desc.Process)
            {
                this.StartTracking(new SessionProcessGroup(this, info));

                this._appliedProcess = true;
            }
        }

        protected override IEnumerable<UsageToken> InspectResource(TimeSpan interval)
        {
            if (session.IsRemoteConnected)
            {
                yield return new SessionUsageToken(session.UserName, session.ClientName);
            }
            else if (session.IsConnected && session.IdleTime is TimeSpan time)
            {
                if (time < interval)
                {
                    yield return new SessionUsageToken(session.UserName);
                }
            }
            //else if (session.IsConsoleConnected && session.IsLocked != true) // fallback
            //{
            //    yield return new SessionUsageToken(session.UserName);
            //}

            foreach (var token in base.InspectResource(interval))
                    yield return token;
        }

        [ActionHandler("lock")]
        internal void HandleActionLock() => session.Lock();
        [ActionHandler("logout")]
        internal void HandleActionLogout() => session.Logoff();
        [ActionHandler("disconnect")]
        internal void HandleActionDisconnect() => session.Disconnect();

        public void TriggerLogon()
        {
            TriggerEvent(nameof(Login));

            if (session.IsRemoteConnected)
            {
                TriggerEvent(nameof(RemoteLogin));
            }
            else if (session.IsConsoleConnected)
            {
                TriggerEvent(nameof(ConsoleLogin));
            }
        }
    }
}
