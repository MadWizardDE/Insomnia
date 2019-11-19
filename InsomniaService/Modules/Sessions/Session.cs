using Cassia;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Linq;
using System.Timers;

namespace MadWizard.Insomnia.Service.Sessions
{
    public class Session : ISession
    {
        ITerminalServer _tsServer;

        internal Session(ITerminalServer tsServer, int sid)
        {
            _tsServer = tsServer;

            Id = sid;

            Security = new SessionSecurity(this);
        }


        private ITerminalServicesSession TSSession => _tsServer.GetSession(Id);


        public int Id { get; }

        public string Name => TSSession.WindowStationName;

        public string UserName => TSSession.UserName;

        public string ClientName => TSSession.ClientName;

        public ConnectionState ConnectionState => (ConnectionState)TSSession.ConnectionState;

        public string ClientUser
        {
            get
            {
                string user = ClientName;
                if (ClientName.Length > 0 && UserName.Length > 0)
                    user += "\\";
                user += UserName;
                return user;
            }
        }

        internal bool IsUserSession => TSSession.UserAccount != null;

        public bool IsConsoleConnected => SessionManager.WTSGetActiveConsoleSessionId() == Id;
        public bool IsRemoteConnected => ClientName.Length > 0;

        public bool? IsLocked { get; internal set; }

        public bool? IsIdle { get; internal set; }
        public long? IdleTime { get; internal set; }

        public ISession.ISessionSecurity Security { get; set; }

        public override string ToString()
        {
            string connection = "disconnected";
            if (IsConsoleConnected)
                connection = "console";
            if (IsRemoteConnected)
                connection = "remote";

            return $"Session[id={Id}, name={UserName}, {connection}]";
        }

        public class SessionSecurity : ISession.ISessionSecurity
        {
            const string SID_GROUP_ADMINISTRATORS = "S-1-5-32-544";
            const string SID_GROUP_USERS = "S-1-5-32-545";

            ISession _session;

            TemporarySessionLease? _lease;

            Timer _timerNotifyImpersonationChange;

            internal SessionSecurity(ISession session)
            {
                _session = session;
            }

            private TemporarySessionLease? Lease
            {
                get
                {
                    if (_lease != null && DateTime.Now > _lease.Value.End)
                        _lease = null;

                    return _lease;
                }

                set => _lease = value;
            }

            public ISession Session => Session;
            public ISession Impersonation => Lease?.Session;

            private WindowsPrincipal Principal
            {
                get
                {
                    SessionManager.WTSQueryUserToken((uint)(Impersonation ?? _session).Id, out var token);

                    return new WindowsPrincipal(new WindowsIdentity(token));
                }
            }
            public bool IsPrincipalAdministrator
            {
                get => Principal.UserClaims.Where(c => c.Value.Contains(SID_GROUP_ADMINISTRATORS)).Count() > 0;
            }
            public bool IsPrincipalUser
            {
                get => Principal.UserClaims.Where(c => c.Value.Contains(SID_GROUP_USERS)).Count() > 0;
            }

            public event EventHandler ImpersonationChanged;

            internal void Impersonate(ISession session, TimeSpan? duration = null)
            {
                if (session == _session)
                    throw new ArgumentException("Cannot impersonate as self");

                if (session != null)
                {
                    if (Impersonation != session)
                    {
                        _lease = new TemporarySessionLease(session, duration);

                        if (duration is TimeSpan interval)
                        {
                            _timerNotifyImpersonationChange?.Stop();
                            _timerNotifyImpersonationChange?.Dispose();
                            _timerNotifyImpersonationChange = new Timer(interval.TotalMilliseconds);
                            _timerNotifyImpersonationChange.AutoReset = false;
                            _timerNotifyImpersonationChange.Elapsed += Timer_Elapsed;
                            _timerNotifyImpersonationChange.Start();
                        }

                        ImpersonationChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
                else if (_lease != null)
                {
                    if (duration == null || (DateTime.Now - _lease.Value.Start) > duration.Value)
                    {
                        _lease = null;

                        _timerNotifyImpersonationChange?.Stop();
                        _timerNotifyImpersonationChange?.Dispose();
                        _timerNotifyImpersonationChange = null;

                        ImpersonationChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }

            private void Timer_Elapsed(object sender, EventArgs args)
            {
                Impersonate(null);
            }

            private struct TemporarySessionLease
            {
                internal TemporarySessionLease(ISession session, TimeSpan? duration)
                {
                    Session = session;
                    Start = DateTime.Now;
                    Duration = duration;
                }

                internal ISession Session { get; }

                internal DateTime Start { get; }
                internal TimeSpan? Duration { get; }
                internal DateTime? End => Duration != null ? Start + Duration : null;
            }
        }
    }

    public enum ConnectionState
    {
        //
        // Summary:
        //     A user is logged on to the session.
        Active = 0,
        //
        // Summary:
        //     A client is connected to the session.
        Connected = 1,
        //
        // Summary:
        //     The session is in the process of connecting to a client.
        ConnectQuery = 2,
        //
        // Summary:
        //     This session is shadowing another session.
        Shadowing = 3,
        //
        // Summary:
        //     The session is active, but the client has disconnected from it.
        Disconnected = 4,
        //
        // Summary:
        //     The session is waiting for a client to connect.
        Idle = 5,
        //
        // Summary:
        //     The session is listening for connections.
        Listening = 6,
        //
        // Summary:
        //     The session is being reset.
        Reset = 7,
        //
        // Summary:
        //     The session is down due to an error.
        Down = 8,
        //
        // Summary:
        //     The session is initializing.
        Initializing = 9
    }

}