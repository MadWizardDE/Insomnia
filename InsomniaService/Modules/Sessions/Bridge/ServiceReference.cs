using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.Sessions
{
    abstract class ServiceReference
    {
        protected ServiceReference(ISession session)
        {
            Session = session;
        }

        public ISession Session { get; private set; }
    }

    class ServiceReference<T> : ServiceReference, IServiceReference<T> where T : class
    {
        T _service;

        internal ServiceReference(ISession session, T service) : base(session)
        {
            _service = service;
        }

        T IServiceReference<T>.Service => _service;
    }
}