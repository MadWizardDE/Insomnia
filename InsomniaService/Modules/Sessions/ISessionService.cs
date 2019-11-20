using System;
using System.Collections.Generic;
using System.Text;


namespace MadWizard.Insomnia.Service.Sessions
{
    public interface ISessionService<T> : IEnumerable<IServiceReference<T>> where T : class
    {
        event EventHandler<SessionEventArgs> ServiceStarted;
        event EventHandler<SessionEventArgs> ServiceStopped;

        T SelectSession(ISession session);
        T SelectSession(int sessionID);
    }

    public interface IServiceReference<T> where T : class
    {
        ISession Session { get; }

        T Service { get; }
    }
}