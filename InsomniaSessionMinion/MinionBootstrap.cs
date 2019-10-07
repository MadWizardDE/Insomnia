using MadWizard.Insomnia.Service.Sessions;
using NamedPipeWrapper;
using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;
using System.Threading;
using System.Diagnostics;

using Timer = System.Timers.Timer;

namespace MadWizard.Insomnia.Minion
{
    class MinionBootstrap : IDisposable
    {
        const string CMD_STARTUP_DELAY = "-StartupDelay=";
        const string CMD_DEBUG_LOG = "-DebugLog";

        int _startupTimeout;

        ManualResetEvent _finishedWaiter;

        public MinionBootstrap(string[] args, int timeout = 5000)
        {
            _startupTimeout = timeout;

            foreach (string arg in args)
            {
                if (arg.StartsWith(CMD_STARTUP_DELAY))
                {
                    int startupDelay = int.Parse(arg.Replace(CMD_STARTUP_DELAY, ""));

                    Thread.Sleep(startupDelay);
                }
                else if (arg.StartsWith(CMD_DEBUG_LOG))
                {
                    DebugLogging = true;

                    //Debug.Listeners.Add(new TraceListener(new FileInfo("helper.log")));
                }
            }
        }

        public NamedPipeClient<Message> PipeClient { get; private set; }

        public SessionMinionConfig Config { get; private set; }

        public bool DebugLogging { get; private set; }

        public void WaitForStartup()
        {
            using (var waiter = new ManualResetEvent(false))
            {
                Exception exception = null;

                #region PipeClient/Timer Callbacks
                void PipeClient_ServerMessage(NamedPipeConnection<Message, Message> connection, Message message)
                {
                    if (message is StartupMessage startupMessage)
                    {
                        Config = startupMessage.Config;

                        waiter.Set();

                        /*
                         * Wir halten die NamedPipe-Verarbeitung solange an, bis wir das Signal kriegen,
                         * dass der Boot-Prozess abgeschlossen ist.
                         */
                        (_finishedWaiter = new ManualResetEvent(false)).WaitOne();
                    }
                    else if (message is TerminateMessage)
                    {
                        exception = new StartupException("Terminated");

                        waiter.Set();
                    }
                    else
                    {
                        exception = new StartupException($"Unexpected Message = {message.GetType().Name}");

                        waiter.Set();
                    }
                }
                void PipeClient_Disconnected(NamedPipeConnection<Message, Message> connection)
                {
                    exception = new StartupException("Disconnected");

                    waiter.Set();
                }
                void PipeClient_Error(Exception error)
                {
                    exception = new StartupException("Error", error);

                    waiter.Set();
                }
                #endregion

                PipeClient = new NamedPipeClient<Message>(Message.PIPE_NAME);
                PipeClient.ServerMessage += PipeClient_ServerMessage;
                PipeClient.Disconnected += PipeClient_Disconnected;
                PipeClient.Error += PipeClient_Error;

                PipeClient.Start();

                PipeClient.PushMessage(new IncarnationMessage(Process.GetCurrentProcess().Id));

                if (!waiter.WaitOne(_startupTimeout))
                    exception = new StartupException("Timeout");

                try
                {
                    if (exception != null)
                        throw exception;
                }
                catch (Exception)
                {
                    try { PipeClient.Stop(); } catch { }

                    throw;
                }
                finally
                {
                    PipeClient.Error -= PipeClient_Error;
                    PipeClient.Disconnected -= PipeClient_Disconnected;
                    PipeClient.ServerMessage -= PipeClient_ServerMessage;
                }
            }
        }

        void IDisposable.Dispose()
        {
            if (_finishedWaiter != null)
            {
                _finishedWaiter.Set();
                _finishedWaiter.Dispose();
            }
        }

        internal class StartupException : Exception
        {
            internal StartupException(string message) : base(message)
            {

            }

            internal StartupException(string message, Exception exception) : base(message, exception)
            {

            }
        }
    }
}