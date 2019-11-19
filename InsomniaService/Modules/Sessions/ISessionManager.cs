using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.Sessions
{
    public interface ISessionManager
    {
        bool ConsoleLocked { get; }
        bool ConsoleActive { get; }

        ISession this[int sid] { get; }
        IEnumerable<ISession> Sessions { get; }
        ISession ConsoleSession { get; }

        event EventHandler<SessionEventArgs> ConsoleSessionChanged;

        event EventHandler<SessionLoginEventArgs> UserLogin;
        event EventHandler<SessionEventArgs> UserIdle;
        event EventHandler<SessionEventArgs> UserPresent;
        event EventHandler<SessionEventArgs> UserLogout;

        ISession FindSessionByID(int sid);
        ISession FindSessionByUserName(string user);

        //ISession CreateSession(string username, string password);

        void ConnectSession(ISession source, ISession target, TimeSpan? keepPrivileges = null);
        //void DisconnectSession(ISession session);
        //void DestroySession(ISession session);
    }

    public class SessionEventArgs : EventArgs
    {
        internal SessionEventArgs(Session session)
        {
            Session = session;
        }

        public ISession Session { get; private set; }
    }
    public class SessionLoginEventArgs : SessionEventArgs
    {
        internal SessionLoginEventArgs(Session session, bool sessionCreated) : base(session)
        {
            IsSessionCreated = sessionCreated;
        }

        public bool IsSessionCreated { get; private set; }
    }
}