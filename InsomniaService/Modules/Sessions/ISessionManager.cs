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

        event EventHandler<UserEventArgs> ConsoleSessionChanged;

        event EventHandler<UserLoginEventArgs> UserLogin;
        event EventHandler<UserEventArgs> UserIdle;
        event EventHandler<UserEventArgs> UserPresent;
        event EventHandler<UserEventArgs> UserLogout;

        ISession FindSessionByID(int sid);
        ISession FindSessionByUserName(string user);

        //ISession CreateSession(string username, string password);

        void ConnectSession(ISession source, ISession target);
        //void DisconnectSession(ISession session);
        //void DestroySession(ISession session);
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