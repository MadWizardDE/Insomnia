using Autofac;
using MadWizard.Insomnia.Processes.Manager;
using MadWizard.Insomnia.Service;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using static MadWizard.Insomnia.Session.Manager.TerminalServicesSession;

namespace MadWizard.Insomnia.Session.Manager
{
    public partial class TerminalServicesManager : ISessionManager, IDisposable
    {
        private Dictionary<uint, TerminalServicesSession>? _sessions; // lazy init

        public required ILogger<TerminalServicesManager> Logger { protected get; init; }

        public required ProcessManager ProcessManager { protected get; init; }

        public TerminalServicesManager(WindowsService? service = null)
        {
            if (service != null)
            {
                service.SessionChanged += Service_SessionChanged;
            }
        }

        protected Dictionary<uint, TerminalServicesSession> Sessions
        {
            get
            {
                if (_sessions == null )
                {
                    _sessions = EnumerateSessions()
                        .Select(MaybeConfigureSession)
                        .Where(s => s != null).Select(s => s!)
                        .ToDictionary(s => s.Id);

                    Logger.LogDebug($"Startup of {GetType().Name} complete.");
                }

                return _sessions;
            }
        }

        public event EventHandler<ISession>? UserLogon;
        public event EventHandler<ISession>? RemoteConnect;
        public event EventHandler<ISession>? ConsoleConnect;
        public event EventHandler<ISession>? RemoteDisconnect;
        public event EventHandler<ISession>? ConsoleDisconnect;
        public event EventHandler<ISession>? UserLogoff;

        public ISession this[uint sid] => Sessions[sid];

        public ISession? ConsoleSession
        {
            get
            {
                var consoleSID = WTSGetActiveConsoleSessionId();

                return Sessions.TryGetValue(consoleSID, out var session) ? session : null;
            }

            set
            {
                if (value != null)
                {
                    ((TerminalServicesSession)value).ConnectToConsole().Wait();
                }
                else
                {
                    this.ConsoleSession?.Disconnect().Wait();
                }
            }
        }

        public ISession? FindSessionByID(uint sid)
        {
            return Sessions.TryGetValue(sid, out var session) ? session : null;
        }
        public ISession? FindSessionByUserName(string user)
        {
            return Sessions.Values.Where(s => s.UserName.Equals(user, StringComparison.InvariantCultureIgnoreCase)).SingleOrDefault();
        }

        public IEnumerator<ISession> GetEnumerator()
        {
            return Sessions.Values.GetEnumerator();
        }

        private async void Service_SessionChanged(object? sender, SessionChangeDescription desc)
        {
            uint sid = (uint)desc.SessionId;

            Sessions.TryGetValue(sid, out var session);

            Logger.LogDebug($"{desc.Reason} -> {(session != null ? session : desc.SessionId)}");

            switch (desc.Reason)
            {
                case SessionChangeReason.SessionLogon:
                    if (session == null && MaybeConfigureSession(sid) is TerminalServicesSession s)
                    {
                        Sessions[sid] = session = s;

                        UserLogon?.Invoke(this, session);
                    }
                    break;

                case SessionChangeReason.SessionLogoff:
                    if (session != null)
                    {
                        Sessions.Remove(sid);

                        UserLogoff?.Invoke(this, session);

                        session.Dispose();
                    }
                    break;

                case SessionChangeReason.RemoteConnect:
                    if (session != null)
                        RemoteConnect?.Invoke(this, session);
                    break;
                case SessionChangeReason.ConsoleConnect:
                    if (session != null)
                        ConsoleConnect?.Invoke(this, session);
                    break;
                case SessionChangeReason.RemoteDisconnect:
                    if (session != null)
                        RemoteDisconnect?.Invoke(this, session);
                    break;
                case SessionChangeReason.ConsoleDisconnect:
                    if (session != null)
                        ConsoleDisconnect?.Invoke(this, session);
                    break;

                case SessionChangeReason.SessionLock:
                    if (session != null)
                        session.IsLocked = true;
                    break;

                case SessionChangeReason.SessionUnlock:
                    if (session != null)
                        session.IsLocked = false;
                    break;
            }
        }

        protected TerminalServicesSession? MaybeConfigureSession(uint sid)
        {
            var info = QuerySessionInformation<WTSINFO>(sid, WTS_INFO_CLASS.WTSSessionInfo);

            // Filter: Service-Session
            if (info.SessionId == 0 || info.WinStationName == "Services")
                return null;
            // Filter: NonUser-Session
            if (string.IsNullOrEmpty(info.UserName))
                return null;

            var session = ConfigureSession(sid);

            Logger.LogDebug($"Successfully configured new {session}");

            return session;
        }

        protected virtual TerminalServicesSession ConfigureSession(uint sid)
        {
            return new TerminalServicesSession(sid)
            {
                Processes = ProcessManager.WithSessionId(sid)
            };
        }

        public virtual void Dispose()
        {
            foreach (var session in Sessions.Values)
                session.Dispose();
        }
    }
}
