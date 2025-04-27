using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Network.Services;


namespace MadWizard.Insomnia.Network.Host
{
    public class VirtualHost : NetworkHost
    {
        internal IVirtualHost VHost { get; init; }

        private event EventInvocation? Access;

        internal VirtualHost(VirtualHostInfo info, IVirtualHost vhost) : base(info)
        {
            VHost = vhost;

            AddEventAction(nameof(Idle), info.OnIdle);
            AddEventAction(nameof(Access), info.OnAccess);

            foreach (var serviceInfo in info.TCPService)
                this.StartTracking(new TCPService(serviceInfo));
            foreach (var serviceInfo in info.HTTPService)
                this.StartTracking(new HTTPService(serviceInfo));
            foreach (var service in this)
                service.Access += Service_Access;

            if (HostMAC == null)
            {
                HostMAC = vhost.Address;
            }
        }

        private void Service_Access(Event eventObj)
        {
            TriggerEvent(nameof(Access));
        }

        #region Action Handlers
        [ActionHandler("start")]
        internal void HandleActionStart()
        {
            if (VHost.State == VirtualHostState.Stopped || VHost.State == VirtualHostState.Suspended)
            {
                VHost.Start();
            }
        }

        [ActionHandler("suspend")]
        internal void HandleActionSuspend()
        {
            if (VHost.State == VirtualHostState.Running)
            {
                VHost.Suspend();
            }
        }

        [ActionHandler("stop")]
        internal void HandleActionStop()
        {
            if (VHost.State == VirtualHostState.Running)
            {
                VHost.Stop();
            }
        }
        #endregion
    }
}