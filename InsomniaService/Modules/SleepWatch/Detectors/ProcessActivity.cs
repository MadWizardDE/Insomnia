using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Sessions;
using MadWizard.Insomnia.Service.SleepWatch;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using static MadWizard.Insomnia.Configuration.SleepWatchConfig.ActivityDetectorConfig;

namespace MadWizard.Insomnia.Service.SleepWatch.Detector
{
    class ProcessActivity : ActivityDetector.IDetector
    {
        ProcessActivityConfig _config;

        ISessionManager _sessionManager;

        IDictionary<int, IDictionary<string, ActivityGroup>> _groups;

        public ProcessActivity(InsomniaConfig config, ISessionManager sessionManager)
        {
            _config = config.SleepWatch.ActivityDetector.ProcessActivity;
            _sessionManager = sessionManager;

            _groups = new Dictionary<int, IDictionary<string, ActivityGroup>>();
        }

        [Autowired]
        ILogger<ProcessActivity> Logger { get; set; }

        (string[] tokens, bool busy) ActivityDetector.IDetector.Scan()
        {
            var tokenList = new HashSet<string>();
            var processList = Process.GetProcesses();

            lock (_groups)
            {
                foreach (var session in _sessionManager.Sessions)
                {
                    if (!_groups.TryGetValue(session.Id, out var sessionGroup))
                        _groups[session.Id] = sessionGroup = new Dictionary<string, ActivityGroup>();

                    foreach (var groupName in _config.Process.Keys)
                    {
                        var groupConfig = _config.Process[groupName];

                        if (!sessionGroup.TryGetValue(groupName, out var group))
                            sessionGroup[groupName] = group = new ActivityGroup(groupConfig.ProcessName, groupConfig.WithChildren);

                        var usage = group.Measure(processList.Where(p => p.SessionId == session.Id));

                        if (group.Active = (usage > groupConfig.Threshold))
                        {
                            tokenList.Add($"<<{session.UserName}::{groupConfig.Name}:{usage:0.00}%>>");
                        }
                    }
                }
            }

            return (tokenList.ToArray(), tokenList.Count > 0);
        }

        public bool HasActivity(int sessionId, string name)
        {
            lock(_groups)
            {
                if (_groups.ContainsKey(sessionId) && _groups[sessionId].ContainsKey(name))
                    return _groups[sessionId][name].Active;

                return false;
            }
        }
    }

    internal class ActivityGroup
    {
        private readonly string processName;
        private readonly bool withChildren;

        private DateTime lastMeasureTime;
        private TimeSpan lastProcessorTime;

        internal ActivityGroup(string processName, bool withChildren)
        {
            this.processName = processName;
            this.withChildren = withChildren;

            this.lastMeasureTime = DateTime.UtcNow;
            this.lastProcessorTime = MeasureProcessorTime(Process.GetProcesses());
        }

        internal bool Active { get; set; } = false;

        internal double Measure(IEnumerable<Process> processList)
        {
            DateTime measureTime = DateTime.UtcNow;
            TimeSpan processorTime = MeasureProcessorTime(processList);

            var deltaProcessorTime = (processorTime - lastProcessorTime).TotalMilliseconds;
            lastProcessorTime = processorTime;
            var deltaMeasureTime = (measureTime - lastMeasureTime).TotalMilliseconds;
            lastMeasureTime = measureTime;

            return (deltaProcessorTime / (Environment.ProcessorCount * deltaMeasureTime)) * 100;
        }

        private TimeSpan MeasureProcessorTime(IEnumerable<Process> processList)
        {
            TimeSpan processorTime = TimeSpan.Zero;

            var parentProcessList = new List<Process>();

            foreach (var proc in processList)
            {
                if (proc.ProcessName != this.processName)
                    continue;

                processorTime += proc.TotalProcessorTime;

                parentProcessList.Add(proc);
            }

            if (this.withChildren)
                foreach (var proc in processList)
                {
                    if (proc.ProcessName == this.processName)
                        continue;

                    foreach (var parentProc in parentProcessList)
                        if (proc.HasParentProcessId(parentProc.Id))
                            processorTime += proc.TotalProcessorTime;
                }

            return processorTime;
        }
    }
}

internal static class ProcessExtension
{
    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref PROCESS_BASIC_INFORMATION processInformation, int processInformationLength, out int returnLength);

    internal static bool HasParentProcessId(this Process proc, int id)
    {
        while ((proc = proc.GetParentProcess()) != null)
            if (proc.Id == id)
                return true;

        return false;
    }

    internal static Process GetParentProcess(this Process proc)
    {
        try
        {
            var info = new PROCESS_BASIC_INFORMATION();

            try
            {
                int status = NtQueryInformationProcess(proc.Handle, 0, ref info, Marshal.SizeOf(info), out _);
                if (status != 0)
                    throw new Win32Exception(status);
            }
            catch (Win32Exception e) when (e.NativeErrorCode == 5)
            {
                return null; // Zugriff verweigert
            }

            return Process.GetProcessById(info.InheritedFromUniqueProcessId.ToInt32());
        }
        catch (SystemException e) when (e is ArgumentException || e is InvalidOperationException)
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_BASIC_INFORMATION
    {
        internal IntPtr Reserved1;
        internal IntPtr PebBaseAddress;
        internal IntPtr Reserved2_0;
        internal IntPtr Reserved2_1;
        internal IntPtr UniqueProcessId;
        internal IntPtr InheritedFromUniqueProcessId;
    }
}