using MadWizard.Insomnia.Processes.Manager;
using MadWizard.Insomnia.Session.Manager;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Test
{
    internal class Test(ISessionManager manager) : BackgroundService
    {
        public required ILogger<Test> Logger { private get; init; }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            foreach (var session in manager)
            {
                Logger.LogInformation(session.UserName);
            }

            //while (!stoppingToken.IsCancellationRequested)
            //{
            //    foreach (var session in manager)
            //    {
            //        //var time = (session as TerminalServicesSession).LastInputTime;

            //        //Logger.LogInformation($"{session.UserName} = " + time);
            //    }

            //    await Task.Delay(5000);
            //}


            //while (!stoppingToken.IsCancellationRequested)
            //{
            //    var session = manager.ConsoleSession;



            //    Logger.LogInformation("Hallo Welt!");

            //    await Task.Delay(1000, stoppingToken);
            //}

        }
    }
}
