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
        public event EventHandler<SessionEventArgs> ServiceStarted
        {
            add => _sessionService.ServiceStarted += value;
            remove => _sessionService.ServiceStarted -= value;
        }
        public event EventHandler<SessionEventArgs> ServiceStopped
        {
            add => _sessionService.ServiceStopped += value;
            remove => _sessionService.ServiceStopped -= value;
        }

        T ISessionService<T>.SelectSession(ISession session)
        {
            return _sessionService.SelectSession(session);
        }
        T ISessionService<T>.SelectSession(int sessionID)
        {
            return _sessionService.SelectSession(sessionID);
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