using System;
using System.ComponentModel.DataAnnotations;
using System.Resources;
using System.Threading;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Network.Services;
using MadWizard.Insomnia.Service.Duo.Configuration;
using MadWizard.Insomnia.Session;
using MadWizard.Insomnia.Session.Manager;
using Microsoft.Win32;
using PacketDotNet;

namespace MadWizard.Insomnia.Service.Duo.Manager
{
    public class DuoInstance : ResourceMonitor<NetworkService>
    {
        private bool? _running = null;
        private ISession? _session = null;

        private readonly RegistryKey Key;

        internal readonly SemaphoreSlim Semaphore = new(1, 1);

        public DuoInstance(DuoInstanceInfo info, RegistryKey key)
        {
            AddEventAction(nameof(Demand), info.OnDemand);
            AddEventAction(nameof(Idle), info.OnIdle);

            AddEventAction(nameof(Login), info.OnLogin);
            AddEventAction(nameof(Started), info.OnStart);
            AddEventAction(nameof(Stopped), info.OnStop);
            AddEventAction(nameof(Logoff), info.OnLogoff);

            Key = key;
        }

        public string Name => Key.Name.Split('\\').Last();

        public int Port => Key.GetValue("Port") is int port ? port : throw new ArgumentNullException("Port");

        public string UserName => Key.GetValue("UserName") is string name ? name : throw new ArgumentNullException("UserName");

        public uint? SessionID
        {
            get
            {
                return (uint?)(Key.GetValue("SessionId") as int?);
            }

            set
            {
                if (value != null)
                    Key.SetValue("SessionId", value);
                else if (Key.GetValue("SessionId") != null)
                    Key.DeleteValue("SessionId");
            }
        }

        public bool IsBusy => Semaphore.CurrentCount == 0;

        public bool? IsRunning
        {
            get => _running;

            internal set
            {
                if (_running != value)
                {
                    if (_running != null)
                    {
                        if (value == true)
                            TriggerEvent(nameof(Started));
                        else if (value == false)
                            TriggerEvent(nameof(Stopped));
                    }
                }

                _running = value;
            }
        }

        [EventContext]
        public ISession? Session
        {
            get => _session;

            internal set
            {
                _session = value;

                if (value == null)
                    TriggerEvent(nameof(Logoff));
                else if (value != null)
                    TriggerEvent(nameof(Login));
            }
        }

        public event EventInvocation? Demand;

        public event EventInvocation? Login;
        public event EventInvocation? Started;
        public event EventInvocation? Stopped;
        public event EventInvocation? Logoff;

        public bool HasInitiated(ISession session)
        {
            return this.Name == session.ClientName && this.UserName == session.UserName;
        }

        public override bool StartTracking(NetworkService service)
        {
            service.Access += NetworkService_Access;

            return base.StartTracking(service);
        }

        private void NetworkService_Access(Event eventObj)
        {
            TriggerEvent(nameof(Demand));
        }

        protected override IEnumerable<UsageToken> InspectResource(TimeSpan interval)
        {
            if (base.InspectResource(interval).Any())
            {
                yield return new DuoStreamUsage(Name);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            Key?.Dispose();
        }

        public override string ToString()
        {
            return $"DuoInstance<{Name}>";
        }
    }
}
