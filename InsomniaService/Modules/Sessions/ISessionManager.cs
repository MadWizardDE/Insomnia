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

        event EventHandler<UserLoginEventArgs> UserLogin;
        event EventHandler<UserEventArgs> UserIdle;
        event EventHandler<UserEventArgs> UserPresent;
        event EventHandler<UserEventArgs> UserLogout;

        //ISession CreateSession(string username, string password);
    }

    public class UserEventArgs : EventArgs
    {
        internal UserEventArgs(Session session)
        {
            Session = session;
        }

        public ISession Session { get; private set; }
    }
    public class UserLoginEventArgs : UserEventArgs
    {
        internal UserLoginEventArgs(Session session, bool sessionCreated) : base(session)
        {
            IsSessionCreated = sessionCreated;
        }

        public bool IsSessionCreated { get; private set; }
    }
}