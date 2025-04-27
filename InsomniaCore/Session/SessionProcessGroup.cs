using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Processes;
using MadWizard.Insomnia.Processes.Manager;
using MadWizard.Insomnia.Session.Manager;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Session
{
    public class SessionProcessGroup : ProcessGroup
    {
        [EventContext]
        public ISession Session { get; private init; }

        public SessionProcessGroup(SessionWatch watch, SessionProcessGroupInfo info) : base(info)
        {
            Session = watch.Session;

            // TODO support SessionIdle Events?

            //watch.Idle += (sender, args) => TriggerAction(info.OnSessionIdle);
            //watch.Busy += (sender, args) => ResetActionTimer(info.OnSessionIdle);
        }

        protected override IEnumerable<IProcess> EnumerateProcesses() => Session.Processes;

        protected override ProcessUsage CreateUsageToken(double usage)
        {
            var token = base.CreateUsageToken(usage);

            token.UserName = Session.UserName;

            return token;
        }
    }
}
