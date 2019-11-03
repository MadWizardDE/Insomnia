using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Sessions;
using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.SleepWatch.Detector
{
    class RemoteDesktopDetector : ActivityDetector.IDetector
    {
        ISessionManager _sessionManager;

        public RemoteDesktopDetector(ISessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        (string[] tokens, bool busy) ActivityDetector.IDetector.Scan()
        {
            List<string> tokenList = new List<string>();

            foreach (Session session in _sessionManager.Sessions)
                if (session.ConnectionState == ConnectionState.Active && session.IsRemoteConnected)
                    tokenList.Add($"<{session.ClientName}\\{session.UserName}>");

            return (tokenList.ToArray(), tokenList.Count > 0);
        }
    }
}
