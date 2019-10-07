using MadWizard.Insomnia.Service.Sessions;
using MadWizard.Insomnia.Tools;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace MadWizard.Insomnia.Minion.Services
{
    class UserIdleTimeService : BackgroundService, IUserIdleTimeService
    {
        SessionMinionConfig _config;

        IUserMessenger _messenger;

        public UserIdleTimeService(SessionMinionConfig config, IUserMessenger messenger)
        {
            _config = config;

            _messenger = messenger;
        }

        public long IdleTime => Win32API.IdleTime;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _messenger.SendMessage(new UserIdleTimeMessage(IdleTime));

                await Task.Delay(_config.Interval);
            }
        }
    }
}