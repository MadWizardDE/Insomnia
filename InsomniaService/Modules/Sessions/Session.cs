using Cassia;
using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.Sessions
{
    public class Session : ISession
    {
        ITerminalServer _tsServer;

        int _sid;

        internal Session(ITerminalServer tsServer, int sid)
        {
            _tsServer = tsServer;

            _sid = sid;
        }

        private ITerminalServicesSession TSSession => _tsServer.GetSession(_sid);

        public int Id => TSSession.SessionId;

        public string UserName => TSSession.UserName;

        public string ClientName => TSSession.ClientName;

        public ConnectionState ConnectionState => (ConnectionState)TSSession.ConnectionState;

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

        internal bool IsUserSession => TSSession.UserAccount != null;

        public bool IsConsoleConnected { get; internal set; }
        public bool IsRemoteConnected => ClientName.Length > 0;

        public bool? IsLocked { get; internal set; }

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