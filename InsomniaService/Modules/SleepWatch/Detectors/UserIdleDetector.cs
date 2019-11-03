using Autofac.Features.OwnedInstances;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Sessions;
using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.SleepWatch.Detector
{
    class UserIdleDetector : ActivityDetector.IDetector, IDisposable
    {
        ISessionManager _sessionManager;

        public UserIdleDetector(ISessionManager sessionManager)
        {
            _sessionManager = sessionManager;
            _sessionManager.UserIdle += OnUserEvent;
            _sessionManager.UserPresent += OnUserEvent;
        }

        private void OnUserEvent(object sender, UserEventArgs e)
        {
            // der Wert wird auch im Session-Objekt hinterlegt
        }

        (string[] tokens, bool busy) ActivityDetector.IDetector.Scan()
        {
            List<string> tokenList = new List<string>();

            foreach (ISession session in _sessionManager.Sessions)
            {
                if (session.IsIdle.HasValue && !session.IsIdle.Value)
                {
                    // Ist der Benutzer NICHT bereits über RDP angemeldet?
                    if (!_sessionManager[session.Id].IsRemoteConnected)
                        tokenList.Add("<" + session.UserName + ">");
                }
            }

            return (tokenList.ToArray(), tokenList.Count > 0);
        }

        void IDisposable.Dispose()
        {
            _sessionManager.UserPresent -= OnUserEvent;
            _sessionManager.UserIdle -= OnUserEvent;
        }
    }
}