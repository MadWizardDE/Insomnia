using System;
using System.Collections.Generic;
using System.Text;


namespace MadWizard.Insomnia.Service.Sessions
{
    public interface ISessionService<T> : IEnumerable<IServiceReference<T>> where T : class
    {
        T SelectSession(ISession session);
    }

    public interface IServiceReference<T> where T : class
    {
        ISession Session { get; }

        T Service { get; }
    }
}