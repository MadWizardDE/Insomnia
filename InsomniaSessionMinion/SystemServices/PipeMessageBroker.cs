using Autofac;
using MadWizard.Insomnia.Service;
using MadWizard.Insomnia.Service.Sessions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NamedPipeWrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Minion
{
    class PipeMessageBroker : IUserMessenger, ISystemMessenger
    {
        IHostApplicationLifetime _lifetime;

        ServiceManager _serviceManager;

        NamedPipeClient<Message> _pipeClient;

        public PipeMessageBroker(IHostApplicationLifetime lifetime, ServiceManager serviceManager, NamedPipeClient<Message> pipeClient)
        {
            _lifetime = lifetime;

            _serviceManager = serviceManager;

            _pipeClient = pipeClient;
            _pipeClient.ServerMessage += PipeClient_ServerMessage;
            _pipeClient.Disconnected += PipeClient_Disconnected;
            _pipeClient.Error += PipeClient_Error;
        }

        [Autowired]
        ILogger<PipeMessageBroker> Logger { get; set; }

        void ISystemMessenger.SendMessage(SystemMessage message)
        {
            _pipeClient.PushMessage(message);
        }
        void IUserMessenger.SendMessage(UserMessage message)
        {
            Logger.LogDebug($"Outgoing message -> {message.GetType().Name}");

            _pipeClient.PushMessage(message);
        }

        #region PipeClient
        private void PipeClient_ServerMessage(NamedPipeConnection<Message, Message> connection, Message message)
        {
            Logger.LogDebug($"Incoming message <- {message.GetType().Name}");

            if (message is ServiceMessage svcMessage)
            {
                _serviceManager.HandleMessage(svcMessage);
            }
            else if (message is TerminateMessage)
            {
                Logger.LogInformation("Terminated, Shutting down...");

                _lifetime.StopApplication();
            }
            else
            {
                Logger.LogWarning($"Message-Type >>{message.GetType().Name}<< not recognized!");
            }
        }
        private void PipeClient_Disconnected(NamedPipeConnection<Message, Message> connection)
        {
            Logger.LogError("Disconnected, Shutting down...");

            _lifetime.StopApplication();
        }
        private void PipeClient_Error(Exception exception)
        {
            Logger.LogError(exception, "PipeClient Error");
        }
        #endregion
    }
}