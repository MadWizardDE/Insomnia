using Cassia;
using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.Sessions
{
    public class Session : ISession
    {
        ITerminalServicesSession _tsSession;

        internal Session(ITerminalServicesSession tsSession)
        {
            _tsSession = tsSession;
        }

        public int Id => _tsSession.SessionId;

        public string UserName => _tsSession.UserName;

        public string ClientName => _tsSession.ClientName;

        public ConnectionState ConnectionState => (ConnectionState)_tsSession.ConnectionState;

        public string ClientUser
        {
            get
            {
                string user = ClientName;
                if (ClientName.Length > 0 && UserName.Length > 0)
                    user += "\\";
                user += UserName;
                return user;
            }
        }

        public bool IsRemoteConnected => ClientName.Length > 0;

        public bool? IsIdle { get; internal set; }
        public long? IdleTime { get; internal set; }

    }

    public enum ConnectionState
    {
        //
        // Summary:
        //     A user is logged on to the session.
        Active = 0,
        //
        // Summary:
        //     A client is connected to the session.
        Connected = 1,
        //
        // Summary:
        //     The session is in the process of connecting to a client.
        ConnectQuery = 2,
        //
        // Summary:
        //     This session is shadowing another session.
        Shadowing = 3,
        //
        // Summary:
        //     The session is active, but the client has disconnected from it.
        Disconnected = 4,
        //
        // Summary:
        //     The session is waiting for a client to connect.
        Idle = 5,
        //
        // Summary:
        //     The session is listening for connections.
        Listening = 6,
        //
        // Summary:
        //     The session is being reset.
        Reset = 7,
        //
        // Summary:
        //     The session is down due to an error.
        Down = 8,
        //
        // Summary:
        //     The session is initializing.
        Initializing = 9
    }

}