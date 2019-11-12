using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.Sessions
{
    class SessionServiceReference
    {

    }
    class SessionServiceReference<T> : SessionServiceReference, ISessionService<T>, IDisposable where T : class
    {
        SessionService<T> _sessionService;

        internal SessionServiceReference(SessionService<T> sessionService)
        {
            _sessionService = sessionService;
            _sessionService.AddReference(this);
        }

        #region ISessionService-Methods
        T ISessionService<T>.SelectSession(ISession session)
        {
            return ((ISessionService<T>)_sessionService).SelectSession(session);
        }
        T ISessionService<T>.SelectSession(int sessionID)
        {
            return ((ISessionService<T>)_sessionService).SelectSession(sessionID);
        }

        IEnumerator<IServiceReference<T>> IEnumerable<IServiceReference<T>>.GetEnumerator()
        {
            return ((ISessionService<T>)_sessionService).GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((ISessionService<T>)_sessionService).GetEnumerator();
        }
        #endregion

        public void Dispose()
        {
            _sessionService.RemoveReference(this);
            _sessionService = null;
        }

    }
}