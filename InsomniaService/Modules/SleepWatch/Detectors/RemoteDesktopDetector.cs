using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Sessions;
using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.SleepWatch.Detector
{
    class RemoteDesktopDetector : ActivityDetector.IDetector
    {
        InsomniaConfig _config;

        SessionManager _sessionManager;

        public RemoteDesktopDetector(InsomniaConfig config, SessionManager sessionManager)
        {
            _config = config;

            _sessionManager = sessionManager;
        }

        (string[] tokens, bool busy) ActivityDetector.IDetector.Scan()
        {
            List<string> tokenList = new List<string>();

            if (_config.SleepWatch?.ActivityDetector?.RemoteDesktopConnection != null)
            {
                foreach (Session session in _sessionManager.Sessions)
                    if (session.ConnectionState == ConnectionState.Active)
                        if (session.IsRemoteConnected)
                            tokenList.Add($"<{session.ClientName}\\{session.UserName}>");
            }

            return (tokenList.ToArray(), tokenList.Count > 0);
        }
    }
}
