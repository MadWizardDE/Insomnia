using Autofac;
using MadWizard.Insomnia.Service;
using MadWizard.Insomnia.Service.Sessions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NamedPipeWrapper;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Minion
{
    class PipeMessageBroker : IStartable, IUserMessenger, ISystemMessenger, IDisposable
    {
        IHostApplicationLifetime _lifetime;

        Lazy<IServiceMessageHandler> _serviceMessageHandler;

        NamedPipeClient<Message> _pipeClient;

        public PipeMessageBroker(IHostApplicationLifetime lifetime, Lazy<IServiceMessageHandler> serviceMessageHandler, NamedPipeClient<Message> pipeClient)
        {
            _lifetime = lifetime;

            _serviceMessageHandler = serviceMessageHandler;

            _pipeClient = pipeClient;
            _pipeClient.ServerMessage += PipeClient_ServerMessage;
            _pipeClient.Disconnected += PipeClient_Disconnected;
            _pipeClient.Error += PipeClient_Error;
        }

        [Autowired]
        ILogger<PipeMessageBroker> Logger { get; set; }

        void IStartable.Start()
        {
            Logger.LogDebug($"{nameof(PipeMessageBroker)} started");
        }

        void ISystemMessenger.SendMessage(SystemMessage message)
        {
            Logger.LogDebug($"Outgoing SystemMessage -> {message.GetType().Name}");

            _pipeClient.PushMessage(message);
        }
        void IUserMessenger.SendMessage(UserMessage message)
        {
            Logger.LogDebug($"Outgoing UserMessage -> {message.GetType().Name}");

            _pipeClient.PushMessage(message);
        }

        #region PipeClient
        private void PipeClient_ServerMessage(NamedPipeConnection<Message, Message> connection, Message message)
        {
            Logger.LogDebug($"Incoming Message <- {message.GetType().Name}");

            if (message is ServiceMessage svcMessage)
            {
                _serviceMessageHandler.Value.HandleMessage(svcMessage);
            }
            else if (message is TerminateMessage)
            {
                Logger.LogInformation("Terminated. Shutting down...");

                _lifetime.StopApplication();
            }
            else
            {
                Logger.LogWarning($"Message-Type >>{message.GetType().Name}<< not recognized!");
            }
        }
        private void PipeClient_Disconnected(NamedPipeConnection<Message, Message> connection)
        {
            Logger.LogError("Disconnected. Shutting down...");

            _lifetime.StopApplication();
        }
        private void PipeClient_Error(Exception exception)
        {
            Logger.LogError(exception, "PipeClient Error");
        }

        void IDisposable.Dispose()
        {
            _pipeClient?.Stop();
            _pipeClient = null;

            Logger.LogDebug($"{nameof(PipeMessageBroker)} stopped");
        }
        #endregion
    }
}