using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MadWizard.Insomnia.Network;

namespace MadWizard.Insomnia.Service.Duo.Sunshine
{

    internal class SunshineFallbackService(int port) : SunshineService(port)
    {
        const int WAIT_RETRY_DELAY = 500;

        private TcpListener? _listener;

        public async void WaitForClient()
        {
            _listener = new(IPAddress.Any, PortHTTP);
            _listener.Start();

            while (_listener != null)
                try
                {
                    using TcpClient client = await _listener.AcceptTcpClientAsync();

                    TriggerEvent(nameof(Access));
                }
                catch (SocketException e)
                {
                    //Console.Write(e.ToString());

                    // port is dead
                }
                finally
                {
                    await Task.Delay(WAIT_RETRY_DELAY);
                }
        }

        public void StopWaiting()
        {
            try
            {
                _listener?.Stop();
            }
            catch (SocketException)
            {
                // irrelevant
            }
            finally
            {
                _listener = null;
            }
        }

        protected override IEnumerable<UsageToken> InspectResource(TimeSpan interval)
        {
            // TODO check tcp connection attempts

            if (IsWaitingForClient)
            {
                if (HasClientConnected)
                    AccessCount++;
            }
            else
            {
                WaitForClient(); // long term safety net
            }

            return base.InspectResource(interval);
        }

        public override void Dispose()
        {
            base.Dispose();

            StopWaiting();
        }
    }
}
