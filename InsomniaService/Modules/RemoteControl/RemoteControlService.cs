using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Autofac.Features.OwnedInstances;
using Grpc.Core;
using MadWizard.Insomnia.Remote;
using MadWizard.Insomnia.Service;
using MadWizard.Insomnia.Service.Sessions;
using MadWizard.Insomnia.Tools;
using Microsoft.Extensions.Logging;
using static MadWizard.Insomnia.Remote.InsomniaService;

namespace MadWizard.Insomnia.RemoteControl
{
    //[Authorize]
    class RemoteControlService : InsomniaServiceBase
    {
        ISessionManager _sessionManager;

        Func<Owned<ISessionService<ISessionControlService>>> _SessionControlServiceFactory;

        public RemoteControlService(ISessionManager sessionManager, Func<Owned<ISessionService<ISessionControlService>>> SessionControlServiceFactory)
        {
            _sessionManager = sessionManager;

            _SessionControlServiceFactory = SessionControlServiceFactory;
        }

        [Autowired]
        ILogger<RemoteControlService> Logger { get; set; }

        #region System
        public override Task<SystemInfo> QuerySystemInfo(SystemRequest request, ServerCallContext ctx)
        {
            var info = new SystemInfo()
            {
                State = SystemInfo.Types.State.Running
            };

            return Task.FromResult(info);
        }
        public override Task<SystemInfo> ChangeSystemState(SystemStateRequest request, ServerCallContext context)
        {
            var info = new SystemInfo();

            try
            {
                switch (request.State)
                {
                    case SystemInfo.Types.State.Unknown:
                        throw new RpcException(new Status(StatusCode.InvalidArgument, "Unknown is not a valid SystemState"));

                    case SystemInfo.Types.State.Hibernate:
                        Win32API.EnterStandby(true);
                        break;

                    case SystemInfo.Types.State.Sleep:
                        Win32API.EnterStandby(false);
                        break;
                }

                info.State = request.State;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "ChangeSystemState failed");

                throw new RpcException(new Status(StatusCode.Internal, e.Message));
            }

            return Task.FromResult(info);
        }
        public override Task<ServiceResponse> StopSystem(SystemStopRequest request, ServerCallContext context)
        {
            var shutdown = Process.Start(new ProcessStartInfo("shutdown", "/s /t 0")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });

            shutdown.WaitForExit();

            if (shutdown.ExitCode != 0)
                throw new RpcException(new Status(StatusCode.Unavailable, $"shutdown.exe -> {shutdown.ExitCode}"));

            return Task.FromResult(new ServiceResponse());
        }
        #endregion

        #region UserSession
        public override Task<UserSessionList> ListUserSessions(UserSessionListRequest request, ServerCallContext context)
        {
            var list = new UserSessionList();

            foreach (ISession session in _sessionManager.Sessions)
            {
                list.Sessions.Add(CreateUserSessionInfo(session));
            }

            return Task.FromResult(list);
        }
        public override Task<UserSessionInfo> StartUserSession(UserSessionStartRequest request, ServerCallContext context)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ""));
        }
        public override Task<UserSessionInfo> QueryUserSessionInfo(UserSessionRequest request, ServerCallContext context)
        {
            return Task.FromResult(CreateUserSessionInfo(_sessionManager[request.SessionID]));
        }
        public override async Task<UserSessionInfo> ChangeUserSessionState(UserSessionStateRequest request, ServerCallContext context)
        {
            ISession session = _sessionManager[request.SessionID];

            if (request.Locked)
            {
                using var svc = _SessionControlServiceFactory();

                await svc.Value.SelectSession(session.Id).Lock();
            }
            else
            {
                //Process.GetCurrentProcess().Se
            }

            switch (request.State)
            {
                case UserSessionInfo.Types.State.Unknown:
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Unknown is not a valid SessionState"));

                case UserSessionInfo.Types.State.ConsoleConnected:
                    // TODO
                    break;
                case UserSessionInfo.Types.State.RemoteConnected:
                    // TODO
                    break;

                case UserSessionInfo.Types.State.Disconnected:
                    // TODO
                    break;
            }

            return CreateUserSessionInfo(_sessionManager[request.SessionID]);
        }
        public override async Task<UserSessionInfo> StopUserSession(UserSessionStopRequest request, ServerCallContext context)
        {
            using (var svc = _SessionControlServiceFactory())
                await svc.Value.SelectSession(request.SessionID).Logoff();

            return CreateUserSessionInfo(_sessionManager[request.SessionID]);
        }

        private UserSessionInfo CreateUserSessionInfo(ISession session)
        {
            var info = new UserSessionInfo()
            {
                SessionID = session.Id,
                Name = session.UserName,
            };

            switch (session.ConnectionState)
            {
                case ConnectionState.Active:
                case ConnectionState.Connected:
                    if (session.IsRemoteConnected)
                        info.State = UserSessionInfo.Types.State.RemoteConnected;
                    else
                        info.State = UserSessionInfo.Types.State.ConsoleConnected;
                    break;

                case ConnectionState.Disconnected:
                    info.State = UserSessionInfo.Types.State.Disconnected;
                    break;

                default:
                    info.State = UserSessionInfo.Types.State.Unknown;
                    break;
            }

            if (session.IsLocked.HasValue)
                info.LockedKnown = session.IsLocked.Value;
            else
                info.LockedUnkown = true;

            return info;
        }
        #endregion

        #region
        public override Task<UserProcessList> ListUserProcesses(UserProcessListRequest request, ServerCallContext context)
        {
            var list = new UserProcessList();

            foreach (Process proc in Process.GetProcesses())
            {
                list.Processes.Add(CreateUserProcessInfo(proc));
            }

            return Task.FromResult(list);
        }
        public override Task<UserProcessInfo> StartUserProcess(StartUserProcessRequest request, ServerCallContext context)
        {
            throw new RpcException(new Status(StatusCode.Unimplemented, ""));
        }
        public override Task<UserProcessInfo> StopUserProcess(StopUserProcessRequest request, ServerCallContext context)
        {
            var proc = Process.GetProcessById(request.ProcessID);

            if (request.Forced)
            {
                proc.Kill();
            }
            else
            {
                proc.CloseMainWindow();
            }

            return Task.FromResult(CreateUserProcessInfo(proc));
        }

        private UserProcessInfo CreateUserProcessInfo(Process proc)
        {
            var info = new UserProcessInfo()
            {
                ProcessID = proc.Id,
                Name = proc.ProcessName,
                SessionID = proc.SessionId,
            };

            return info;
        }
        #endregion

    }
}