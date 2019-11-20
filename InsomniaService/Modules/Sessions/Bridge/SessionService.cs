using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service.Sessions
{
    internal abstract class SessionService : IDisposable
    {
        List<SessionServiceReference> _externalRefs;

        protected SessionService(Type type)
        {
            ServiceType = type;

            _externalRefs = new List<SessionServiceReference>();
        }

        internal Type ServiceType { get; private set; }

        public int ReferenceCount => _externalRefs.Count;

        public event EventHandler ReferencesChanged;

        internal void AddReference(SessionServiceReference ssRef)
        {
            _externalRefs.Add(ssRef);

            ReferencesChanged?.Invoke(this, EventArgs.Empty);
        }
        internal bool RemoveReference(SessionServiceReference ssRef)
        {
            bool removed = _externalRefs.Remove(ssRef);

            ReferencesChanged?.Invoke(this, EventArgs.Empty);

            return removed;
        }

        internal abstract void AddSession(ISession session);
        internal abstract void RemoveSession(ISession session);

        public abstract void Dispose();
    }

    internal class SessionService<T> : SessionService, ISessionService<T> where T : class
    {
        SessionBridge __bridge;

        IDictionary<ISession, IServiceReference<T>> _sessionRefs;

        public event EventHandler<SessionEventArgs> ServiceStarted;
        public event EventHandler<SessionEventArgs> ServiceStopped;

        internal SessionService(SessionBridge bridge) : base(typeof(T))
        {
            __bridge = bridge;

            _sessionRefs = new ConcurrentDictionary<ISession, IServiceReference<T>>();
        }

        internal override void AddSession(ISession session)
        {
            if (_sessionRefs.ContainsKey(session))
                throw new ArgumentException($"Session {session.Id} already known.");

            var serviceRef = __bridge.AcquireServiceReference<T>(session).Result;

            _sessionRefs[session] = serviceRef;

            ServiceStarted?.Invoke(this, new SessionEventArgs(session));
        }
        public T SelectSession(ISession session)
        {
            if (!_sessionRefs.ContainsKey(session))
                throw new ArgumentException($"Session {session.Id} unknown.");
            if (!_sessionRefs.TryGetValue(session, out var serviceRef))
                throw new SessionNotFoundException(session.Id);

            return serviceRef.Service;
        }
        public T SelectSession(int sessionID)
        {
            foreach (var session in _sessionRefs.Keys)
                if (session.Id == sessionID)
                    return _sessionRefs[session].Service;

            throw new SessionNotFoundException(sessionID);
        }
        internal override void RemoveSession(ISession session)
        {
            if (!_sessionRefs.ContainsKey(session))
                throw new ArgumentException($"Session {session.Id} unknown.");

            __bridge.ReleaseServiceReference(_sessionRefs[session]).Wait();

            _sessionRefs.Remove(session);

            ServiceStopped?.Invoke(this, new SessionEventArgs(session));
        }

        IEnumerator<IServiceReference<T>> IEnumerable<IServiceReference<T>>.GetEnumerator()
        {
            foreach (var serviceRef in _sessionRefs.Values)
                yield return serviceRef;
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<IServiceReference<T>>)this).GetEnumerator();
        }

        public override void Dispose()
        {
            foreach (ISession session in _sessionRefs.Keys.ToArray())
                RemoveSession(session);
        }
    }

    internal class SessionNotFoundException : Exception
    {
        internal SessionNotFoundException(int sessionID) : base($"Session with SessionID {sessionID} was not found.") { }
    }
}