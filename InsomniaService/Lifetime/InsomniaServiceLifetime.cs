using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service.Lifetime
{
    public class InsomniaServiceLifetime : WindowsServiceLifetime
    {
        ILogger<InsomniaServiceLifetime> _logger;

        IEnumerable<IPowerEventHandler> _handlersPowerEvent;
        IEnumerable<ISessionChangeHandler> _handlersSessionChange;

        public InsomniaServiceLifetime(
            /* InsomniaServiceLifetime */
            IEnumerable<IPowerEventHandler> powerEventHandlers, IEnumerable<ISessionChangeHandler> sessionChangeHandler,
            /* WindowsServiceLifetime */
            IHostEnvironment environment, IHostApplicationLifetime applicationLifetime, ILoggerFactory loggerFactory, IOptions<HostOptions> optionsAccessor)
            : base(environment, applicationLifetime, loggerFactory, optionsAccessor)
        {
            _logger = loggerFactory.CreateLogger<InsomniaServiceLifetime>();

            _handlersPowerEvent = powerEventHandlers;
            _handlersSessionChange = sessionChangeHandler;

            InitializeService();
        }

        private void InitializeService()
        {
            CanHandlePowerEvent = true;
            CanHandleSessionChangeEvent = true;
            CanShutdown = true;
        }

        protected override bool OnPowerEvent(PowerBroadcastStatus status)
        {
            if (status == PowerBroadcastStatus.Suspend)
                _logger.LogInformation(InsomniaEventId.STANDBY_ENTER, "Entering Standby");
            if (status == PowerBroadcastStatus.ResumeSuspend)
            {
                //if (this[typeof(PowerBroadcastFallback)] != null)
                //    return true;

                _logger.LogInformation(InsomniaEventId.STANDBY_LEAVE, "Resuming Operation");
            }

            try
            {
                foreach (IPowerEventHandler handler in _handlersPowerEvent)
                    handler.OnPowerEvent(status);
            }
            catch (Exception e)
            {
                _logger.LogError(InsomniaEventId.POWER_EVENT_ERROR, e, "Failed to notify handler");
            }

            return true;
        }

        protected override void OnSessionChange(SessionChangeDescription desc)
        {
            try
            {
                foreach (ISessionChangeHandler handler in _handlersSessionChange)
                    handler.OnSessionChange(desc);
            }
            catch (Exception e)
            {
                _logger.LogError(InsomniaEventId.SESSION_CHANGE_ERROR, e, "Failed to notify handler");
            }
        }
    }
}