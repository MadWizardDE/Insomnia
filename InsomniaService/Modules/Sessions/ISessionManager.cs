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

        event EventHandler<UserEventArgs> UserLogin;
        event EventHandler<UserEventArgs> UserIdle;
        event EventHandler<UserEventArgs> UserPresent;
        event EventHandler<UserEventArgs> UserLogout;

    }

    public class UserEventArgs : EventArgs
    {
        internal UserEventArgs(Session session)
        {
            Session = session;
        }

        public ISession Session { get; private set; }
    }
}