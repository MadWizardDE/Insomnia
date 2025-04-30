using MadWizard.Insomnia.Processes;
using MadWizard.Insomnia.Service.Duo.Configuration;
using MadWizard.Insomnia.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Refit;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using System.Timers;

namespace MadWizard.Insomnia.Service.Duo.Manager
{
    public class DuoManager(DuoConfig config) : BackgroundService, IIEnumerable<DuoInstance>
    {
        const int DEFAULT_TIMEOUT = 30000;
        const string REGISTRY_KEY = "SOFTWARE\\Duo";
        const uint DEFAULT_PORT = 38299;

        public required ILogger<DuoManager> Logger { get; set; }

        internal IDuoWebManager? API;

        private IList<DuoInstance> Instances { get; set; } = [];

        public event EventHandler? Started;
        public event EventHandler? Stopped;

        private uint Port
        {
            get
            {
                using var duo = Registry.LocalMachine!.OpenSubKey(REGISTRY_KEY);

                return duo?.GetValue("Port") is int port ? (uint)port : DEFAULT_PORT;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (config.DuoStreamMonitor != null)
                try
                {
                    ServiceController service = new(config.DuoStreamMonitor.ServiceName);

                    service.Refresh(); // check if available, otherwise -> InvalidOperationException

                    var status = ServiceControllerStatus.Stopped;
                    while (config.DuoStreamMonitor != null && !stoppingToken.IsCancellationRequested)
                    try
                    {
                        Logger.LogTrace("Checking Duo instances...");

                        service.Refresh();

                        switch (service.Status)
                        {
                            case ServiceControllerStatus.Running:
                                if (status == ServiceControllerStatus.Stopped)
                                {
                                    API = RestService.For<IDuoWebManager>("http://localhost:" + Port);

                                    Instances = LoadInstances();

                                    this.Started?.Invoke(this, EventArgs.Empty);
                                }

                                foreach (var instance in this)
                                    if (!instance.IsBusy)
                                        await Refresh(instance);

                                break;

                            case ServiceControllerStatus.Stopped:
                                if (status == ServiceControllerStatus.Running)
                                {
                                    this.Stopped?.Invoke(this, EventArgs.Empty);

                                    foreach (var instance in this)
                                        instance.Dispose();

                                    Instances.Clear();

                                    API = null;
                                }

                                break;

                            case ServiceControllerStatus.StartPending:
                            case ServiceControllerStatus.StopPending:
                                continue;
                        }

                        status = service.Status;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Error checking instances.");
                    }
                    finally
                    {
                        await Task.Delay(config.DuoStreamMonitor.ManagerInterval.Duration, stoppingToken);
                    }
                }
                catch (TaskCanceledException)
                {
                    // ignore
                }
                catch (InvalidOperationException ex)
                {
                    Logger.LogError(ex, "Error checking service.");
                }
        }

        private List<DuoInstance> LoadInstances()
        {
            var instances = new List<DuoInstance>();

            if (config.DuoStreamMonitor != null)
            {
                using RegistryKey instancesKey = OpenInstancesKey();

                foreach (var name in instancesKey.GetSubKeyNames())
                {
                    var info = config.DuoStreamMonitor[name] ?? new DuoInstanceInfo(name);

                    info.OnDemand ??= config.DuoStreamMonitor.OnInstanceDemand;
                    info.OnIdle ??= config.DuoStreamMonitor.OnInstanceIdle;

                    info.OnLogin ??= config.DuoStreamMonitor.OnInstanceLogin;
                    info.OnStart ??= config.DuoStreamMonitor.OnInstanceStarted;
                    info.OnStop ??= config.DuoStreamMonitor.OnInstanceStopped;
                    info.OnLogoff ??= config.DuoStreamMonitor.OnInstanceLogoff;

                    instances.Add(new DuoInstance(info, instancesKey.OpenSubKey(name!, writable: true)!));
                }
            }

            return instances;
        }

        public async Task<bool> Refresh(DuoInstance instance)
        {
            bool? wasRunning = instance.IsRunning, shouldBeRunning = await API.QueryInstance(instance.Name);

            if (shouldBeRunning.Value)
                instance.IsRunning = instance.IsSandboxed || (instance.SessionID != null);
            else
            {
                instance.IsRunning = false;
                instance.SessionID = null;
            }

            if (wasRunning != null && wasRunning != instance.IsRunning)
            {
                Logger.LogInformation($"{instance} {(instance.IsRunning.Value ? "started" : "stopped")} " +
                    $"{(instance.IsBusy ? "successfully" : "manually")}");
            }

            return instance.IsRunning!.Value;
        }

        public async Task Start(DuoInstance instance, int timeout = DEFAULT_TIMEOUT)
        {
            Logger.LogInformation($"Starting {instance}...");

            await API.StartInstance(instance.Name);

            Stopwatch watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeout)
            {
                if (await Refresh(instance))
                {
                    return;
                }

                await Task.Delay(250);
            }

            Logger.LogError($"{instance} failed to start");

            //throw new System.TimeoutException($"{instance} failed to start");
        }

        public async Task Stop(DuoInstance instance, int timeout = 5000)
        {
            Logger.LogInformation($"Stopping {instance}...");

            await API.StopInstance(instance.Name);

            Stopwatch watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeout)
            {
                if (!(await Refresh(instance)))
                {
                    return;
                }

                await Task.Delay(250);
            }

            Logger.LogError($"{instance} failed to stop");

            //throw new System.TimeoutException($"{instance} failed to stop");
        }

        public IEnumerator<DuoInstance> GetEnumerator()
        {
            return Instances.GetEnumerator();
        }
        
        private static RegistryKey OpenInstancesKey()
        {
            using RegistryKey? duo = Registry.LocalMachine!.OpenSubKey(REGISTRY_KEY);

            if (duo != null)
            {
                RegistryKey? instances = duo.OpenSubKey("Instances"); // since Duo 1.5.0

                return instances ?? duo;
            }

            throw new FileNotFoundException(fileName: REGISTRY_KEY, message: "Duo registry key not found");
        }
    }

    internal interface IDuoWebManager
    {
        [Get("/instances/{name}")]
        Task<bool> QueryInstance(string name);

        [Get("/instances/{name}/start")]
        Task StartInstance(string name);

        [Get("/instances/{name}/stop")]
        Task StopInstance(string name);
    }

}
