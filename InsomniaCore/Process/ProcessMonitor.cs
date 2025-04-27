using MadWizard.Insomnia.Configuration;
using Autofac;
using MadWizard.Insomnia.Processes.Manager;
using Microsoft.Extensions.Logging;


namespace MadWizard.Insomnia.Processes
{
    public class ProcessMonitor(ProcessMonitorConfig config, IProcessManager manager) : ResourceMonitor<ProcessGroup>, IStartable
    {
        public required ILogger<ProcessMonitor> Logger { get; set; }

        void IStartable.Start()
        {
            foreach (var info in config.Process)
            {
                StartTracking(new SystemProcessGroup(manager, info));
            }

            Logger.LogDebug("Startup complete");
        }

        private class SystemProcessGroup(IProcessManager manager, ProcessGroupInfo info) : ProcessGroup(info)
        {
            protected override IEnumerable<IProcess> EnumerateProcesses() => manager;
        }
    }
}
