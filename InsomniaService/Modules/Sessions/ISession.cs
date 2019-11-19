using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.Sessions
{
    public interface ISession
    {
        int Id { get; }

        public string Name { get; }

        public string UserName { get; }

        public string ClientName { get; }

        public ConnectionState ConnectionState { get; }

        public string ClientUser { get; }

        public bool IsConsoleConnected { get; }
        public bool IsRemoteConnected { get; }

        public bool? IsLocked { get; }

        public bool? IsIdle { get; }
        public long? IdleTime { get; }

        public ISessionSecurity Security { get; }

        public interface ISessionSecurity
        {
            public ISession Session { get; }
            public ISession Impersonation { get; }

            public bool IsPrincipalAdministrator { get; }
            public bool IsPrincipalUser { get; }

            public event EventHandler ImpersonationChanged;
        }
    }
}