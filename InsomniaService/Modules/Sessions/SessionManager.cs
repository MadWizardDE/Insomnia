using Autofac;
using Autofac.Features.OwnedInstances;
using Cassia;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Lifetime;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;

namespace MadWizard.Insomnia.Service.Sessions
{
    class SessionManager : IStartable, ISessionManager, IPowerEventHandler, ISessionChangeHandler, ISessionMessageHandler<UserIdleTimeMessage>
    {
        InsomniaConfig _config;

        Func<Owned<ISessionService<ISessionControlService>>> _ssSessionControlFactory;

        Func<Owned<ISessionService<IUserIdleTimeService>>> _ssUserIdleFactory;
        Owned<ISessionService<IUserIdleTimeService>> _ssUserIdleService;

        ITerminalServicesManager _tsManager;
        ITerminalServer _tsServer;

        IDictionary<int, Session> _sessions;

        event EventHandler<SessionEventArgs> _eventUserIdle;
        event EventHandler<SessionEventArgs> _eventUserPresent;

        public SessionManager(InsomniaConfig config,
            Func<Owned<ISessionService<ISessionControlService>>> AcquireSessionControlService,
            Func<Owned<ISessionService<IUserIdleTimeService>>> AcquireUserIdleService)
        {
            _config = config;

            _ssSessionControlFactory = AcquireSessionControlService;
            _ssUserIdleFactory = AcquireUserIdleService;

            _tsManager = new TerminalServicesManager();
            _tsServer = _tsManager.GetLocalServer();

            _sessions = PopulateSessions();

            IDictionary<int, Session> PopulateSessions()
            {
                var dict = new Dictionary<int, Session>();

                foreach (ITerminalServicesSession s in _tsServer.GetSessions())
                    if (s.ConnectionState == Cassia.ConnectionState.Active
                        || s.ConnectionState == Cassia.ConnectionState.Connected
                        || s.ConnectionState == Cassia.ConnectionState.Disconnected)
                        if (s.UserAccount != null)
                        {
                            var session = new Session(_tsServer, s.SessionId);

                            dict[s.SessionId] = session;
                        }

                return dict;
            }
        }

        [Autowired]
        ILogger<SessionManager> Logger { get; set; }

        public bool ConsoleLocked => ConsoleSession.IsLocked.HasValue ? ConsoleSession.IsLocked.Value : false;
        public bool ConsoleActive => ConsoleSession.ConnectionState == ConnectionState.Active;

        public ISession this[int sid] => _sessions[sid];
        public IEnumerable<ISession> Sessions => _sessions.Values;
        public ISession ConsoleSession => _sessions.TryGetValue((int)WTSGetActiveConsoleSessionId(), out var session) ? session : null;

        public event EventHandler<SessionEventArgs> ConsoleSessionChanged;

        public event EventHandler<SessionLoginEventArgs> UserLogin;
        public event EventHandler<SessionEventArgs> UserIdle
        {
            add
            {
                AcquireIdleTimeService();

                _eventUserIdle += value;
            }

            remove
            {
                _eventUserIdle -= value;

                ReleaseIdleTimeService();
            }
        }
        public event EventHandler<SessionEventArgs> UserPresent
        {
            add
            {
                AcquireIdleTimeService();

                _eventUserPresent += value;
            }

            remove
            {
                _eventUserPresent -= value;

                ReleaseIdleTimeService();
            }
        }
        public event EventHandler<SessionEventArgs> UserLogout;
        private void AcquireIdleTimeService()
        {
            if (_ssUserIdleService == null)
            {
                _ssUserIdleService = _ssUserIdleFactory();

                PrepareIdleTimeService();
            }
        }
        private void PrepareIdleTimeService()
        {
            if (_ssUserIdleService != null)
            {
                foreach (var instance in _ssUserIdleService.Value)
                {
                    Session session = (Session)instance.Session;

                    session.IsIdle = null;
                    session.IdleTime = instance.Service.IdleTime;
                }
            }
        }
        private void ReleaseIdleTimeService()
        {
            if (_eventUserIdle != null && _eventUserPresent == null)
            {
                foreach (Session session in _sessions.Values)
                {
                    session.IsIdle = null;
                    session.IdleTime = null;
                }

                _ssUserIdleService?.Dispose();
                _ssUserIdleService = null;
            }
        }

        void IStartable.Start()
        {
        }

        void IPowerEventHandler.OnPowerEvent(PowerBroadcastStatus status)
        {
            switch (status)
            {
                case PowerBroadcastStatus.ResumeSuspend:
                    PrepareIdleTimeService();
                    break;
            }

            Session consoleSession = (Session)ConsoleSession;

            string clientUser = consoleSession?.ClientUser;

            if (consoleSession != null && Logger.IsEnabled(LogLevel.Debug))
            {
                var sb = new StringBuilder();
                sb.Append($"PowerEvent: Status={status} ");

                sb.Append("(");
                sb.Append($"Console: SessionId={consoleSession.Id}, ");
                if (clientUser.Length > 0)
                    sb.Append($"User={clientUser}, ");
                sb.Append($"State={consoleSession.ConnectionState}");
                if (consoleSession.ConnectionState == ConnectionState.Active)
                    sb.Append($"|{(ConsoleLocked ? "Locked" : "Unlocked")}");
                sb.Append(")");

                Logger.LogDebug(InsomniaEventId.POWER_EVENT_INFO, sb.ToString());
            }
        }
        void ISessionChangeHandler.OnSessionChange(SessionChangeDescription desc)
        {
            if (!_sessions.TryGetValue(desc.SessionId, out Session session))
                session = new Session(_tsServer, desc.SessionId);

            string clientUser = session.ClientUser;

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                var sb = new StringBuilder();
                sb.Append($"SessionChange: SessionId={desc.SessionId}, Reason={desc.Reason}");

                sb.Append("(");
                if (clientUser.Length > 0)
                    sb.Append($"User={clientUser}, ");
                sb.Append($"State={session.ConnectionState}");
                sb.Append(")");

                Logger.LogDebug(InsomniaEventId.SESSION_CHANGE_INFO, sb.ToString());
            }

            if (session.IsUserSession)
            {
                (session.Security as Session.SessionSecurity).Impersonate(null, TimeSpan.FromSeconds(5));

                if (desc.Reason == SessionChangeReason.SessionLogon
                        || desc.Reason == SessionChangeReason.SessionUnlock && !session.IsRemoteConnected
                        || desc.Reason == SessionChangeReason.ConsoleConnect
                        || desc.Reason == SessionChangeReason.RemoteConnect)

                    if (session.ConnectionState == ConnectionState.Active)
                    {
                        Logger.LogInformation(InsomniaEventId.USER_LOGIN, $"User login: {clientUser} (SID={session.Id})");

                        bool createSession = !_sessions.ContainsKey(desc.SessionId);

                        if (createSession)
                        {
                            _sessions.Add(session.Id, session);
                        }

                        UserLogin?.Invoke(this, new SessionLoginEventArgs(session, createSession));
                    }

                if (desc.Reason == SessionChangeReason.ConsoleConnect || desc.Reason == SessionChangeReason.ConsoleDisconnect)
                {
                    ConsoleSessionChanged?.Invoke(this, new SessionEventArgs(session));
                }

                if (desc.Reason == SessionChangeReason.SessionLock)
                    session.IsLocked = true;
                if (desc.Reason == SessionChangeReason.SessionUnlock)
                    session.IsLocked = false;
            }

            if (desc.Reason == SessionChangeReason.SessionLogoff)
            {
                if (_sessions.Remove(session.Id))
                {
                    Logger.LogInformation(InsomniaEventId.USER_LOGIN, $"User logout: {clientUser} (SID={session.Id})");

                    UserLogout?.Invoke(this, new SessionEventArgs(session));
                }
            }
        }
        void ISessionMessageHandler<UserIdleTimeMessage>.Handle(ISession s, UserIdleTimeMessage message)
        {
            Session session = s as Session;
            bool? lastIdle = session.IsIdle;
            long? lastIdleTime = session.IdleTime;

            session.IdleTime = message.Time;

            if (lastIdleTime != null && lastIdleTime > message.Time)
            {
                session.IsIdle = false;

                if (lastIdle != session.IsIdle)
                    _eventUserPresent?.Invoke(this, new SessionEventArgs(session));
            }
            else if (message.Time > _config.Interval)
            {
                session.IsIdle = true;

                if (lastIdle != session.IsIdle)
                    _eventUserIdle?.Invoke(this, new SessionEventArgs(session));
            }
        }

        ISession ISessionManager.FindSessionByID(int sid)
        {
            return _sessions.Values.Where(s => s.Id.Equals(sid)).SingleOrDefault();
        }
        ISession ISessionManager.FindSessionByUserName(string user)
        {
            return _sessions.Values.Where(s => s.UserName.Equals(user, StringComparison.InvariantCultureIgnoreCase)).SingleOrDefault();
        }

        void ISessionManager.ConnectSession(ISession source, ISession target, TimeSpan? keepPrivileges)
        {
            int sourceID = source.Id;
            int targetID = (target != null ? target.Id : (int)WTSGetActiveConsoleSessionId());

            if (_sessions.TryGetValue(targetID, out Session targetUser))
            {
                if (keepPrivileges != null)
                {
                    ISession impersonationTarget = targetUser.Security.Impersonation ?? targetUser;

                    if (target.Security.Impersonation != source)
                        (source.Security as Session.SessionSecurity).Impersonate(impersonationTarget, keepPrivileges.Value);
                }

                (targetUser.Security as Session.SessionSecurity).Impersonate(null);
            }

            if (target?.IsRemoteConnected ?? false)
            {
                using var control = _ssSessionControlFactory();

                control.Value.SelectSession(targetID).ConnectTo(sourceID);
            }
            else if (!WTSConnectSession((UInt32)sourceID, (UInt32)targetID, "", true))
            {
                int lastError = Marshal.GetLastWin32Error();

                if (lastError > 0)
                    throw new Win32Exception(lastError);
            }
        }

        private void LogUserInfo(int sid)
        {
            WTSQueryUserToken((uint)sid, out var token);

            var identity = new WindowsIdentity(token);
            Logger.LogDebug("Identity-Name: " + identity.Name);
            Logger.LogDebug("Security-Identifier: " + identity.User);
            foreach (var group in identity.Groups)
                Logger.LogDebug("Group-Identifier: " + group);

            var prinz = new WindowsPrincipal(identity);
            Logger.LogDebug("IsUser: " + prinz.IsInRole(WindowsBuiltInRole.User));
            Logger.LogDebug("IsAdmin: " + prinz.IsInRole(WindowsBuiltInRole.Administrator));
        }

        [DllImport("Kernel32.dll")]
        internal static extern UInt32 WTSGetActiveConsoleSessionId();
        [DllImport("Wtsapi32.dll")]
        static extern bool WTSConnectSession(UInt32 LogonId, UInt32 TargetLogonId, String pPassword, bool bWait);
        [DllImport("Wtsapi32.dll")]
        static extern bool WTSDisconnectSession(IntPtr hServer, uint SessionId, bool bWait);
        [DllImport("wtsapi32.dll", SetLastError = true)]
        internal static extern bool WTSQueryUserToken(UInt32 sessionId, out IntPtr Token);

    }
}