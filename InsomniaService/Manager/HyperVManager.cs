using System.Management;
using System.Net.NetworkInformation;

namespace MadWizard.Insomnia.Network.Manager
{
    public class HyperVManager : IVirtualHostManager
    {
        public IVirtualHost? FindHostByName(string name)
        {
            if (HyperVAPI.HasVirtualMachine(name))
            {
                return new HyperVHost(name);
            }

            return null;
        }
    }

    public class HyperVHost(string name) : IVirtualHost
    {
        private const int STATE_CHECK_TIME = 1000; // 1s

        private DateTime? _lastStateCheckTime;
        private VirtualHostState? _lastKnownState;

        VirtualHostState IVirtualHost.State
        {
            get
            {
                if (_lastKnownState == null || (DateTime.Now - _lastStateCheckTime) > TimeSpan.FromMilliseconds(STATE_CHECK_TIME))
                {
                    _lastKnownState = HyperVAPI.GetVirtualMachineState(name) switch
                    {
                        VirtualMachineState.Running => VirtualHostState.Running,
                        VirtualMachineState.EnabledButOffline => VirtualHostState.Suspended,
                        VirtualMachineState.Off => VirtualHostState.Stopped,

                        _ => VirtualHostState.Unknown
                    };

                    _lastStateCheckTime = DateTime.Now;
                }

                return _lastKnownState.Value;
            }
        }

        public PhysicalAddress Address => HyperVAPI.GetVirtualMachineAddress(name);

        async Task IVirtualHost.Start()
        {
            await Task.Run(() => HyperVAPI.StartVirtualMachine(name));
        }

        async Task IVirtualHost.Stop()
        {
            await Task.Run(() => HyperVAPI.StopVirtualMachine(name));
        }

        async Task IVirtualHost.Suspend()
        {
            await Task.Run(() => HyperVAPI.SuspendVirtualMachine(name));
        }
    }

    #region API: HyperV-Management
    file static class HyperVAPI
    {
        static ManagementScope virtualizationScope = new(@"\\.\root\virtualization\v2", null);

        public static void StartVirtualMachine(string name)
        {
            using ManagementObject virtualMachine = GetVirtualMachine(name);

            RequestStateChange(virtualMachine, RequestedState.Enabled);
        }

        public static void StopVirtualMachine(string name)
        {
            using ManagementObject virtualMachine = GetVirtualMachine(name);

            RequestStateChange(virtualMachine, RequestedState.Disabled);
        }

        public static void SuspendVirtualMachine(string name)
        {
            using ManagementObject virtualMachine = GetVirtualMachine(name);

            RequestStateChange(virtualMachine, RequestedState.Offline);
        }

        public static VirtualMachineState GetVirtualMachineState(string name)
        {
            using ManagementObject virtualMachine = GetVirtualMachine(name);

            return (VirtualMachineState)(UInt16)virtualMachine["EnabledState"];
        }

        public static PhysicalAddress GetVirtualMachineAddress(string name)
        {
            using ManagementObject vm = GetVirtualMachine(name);

            var relatedVMSettings = vm.GetRelated("Msvm_VirtualSystemSettingData");

            var relatedEthSettings = relatedVMSettings.First().GetRelated("Msvm_SyntheticEthernetPortSettingData");

            var first = relatedEthSettings.First();

            var adr = (string)first.GetPropertyValue("Address");

            var phy = PhysicalAddress.Parse(adr);

            return phy;
        }

        public static bool HasVirtualMachine(string name)
        {
            try
            {
                GetVirtualMachine(name); return true;
            }
            catch (ManagementException)
            {
                return false;
            }
        }

        private static ManagementObject? GetVirtualMachine(string name)
        {
            //ObjectQuery query = new ObjectQuery($"SELECT * FROM Msvm_ComputerSystem WHERE Caption = \"Virtual Machine\" AND ElementName = \"{name}\"");
            ObjectQuery query = new ObjectQuery($"SELECT * FROM Msvm_ComputerSystem WHERE ElementName = \"{name}\"");
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(virtualizationScope, query))
            using (ManagementObjectCollection collection = searcher.Get())
            {
                if (collection.Count == 0)
                    throw new ManagementException("Unable to find the Virtual Machine.");

                return collection.First();
            }
        }

        private static void RequestStateChange(ManagementObject virtualMachine, RequestedState requestedState)
        {
            using (ManagementBaseObject inputParameters = virtualMachine.GetMethodParameters("RequestStateChange"))
            {
                inputParameters["RequestedState"] = requestedState;
                using (ManagementBaseObject outputParameters = virtualMachine.InvokeMethod("RequestStateChange", inputParameters, null))
                    ValidateOutput(outputParameters);
            }
        }

        private static void ValidateOutput(ManagementBaseObject outputParameters)
        {
            uint errorCode = (uint)outputParameters["ReturnValue"];
            if (errorCode == 4096)
            {
                using (ManagementObject job = new ManagementObject((string)outputParameters["Job"]))
                {
                    while (!IsJobComplete(job["JobState"]))
                    {
                        Thread.Sleep(500);
                        job.Get();
                    }

                    if (!IsJobSuccessful(job["JobState"]))
                    {
                        string errorMessage = "The method failed.";
                        if (!String.IsNullOrWhiteSpace((string)job["ErrorDescription"]))
                            errorMessage = (string)job["ErrorDescription"];

                        throw new ManagementException(errorMessage);
                    }
                }
            }
            else if (errorCode != 0)
            {
                throw new ManagementException(ErrorCodeMeaning(errorCode));
            }
        }

        private static string ErrorCodeMeaning(uint returnValue)
        {
            switch (returnValue)
            {
                case 0: return "Completed with No Error.";
                case 1: return "Not Supported.";
                case 2: return "Failed.";
                case 3: return "Timeout.";
                case 4: return "Invalid Parameter.";
                case 5: return "Invalid State.";
                case 6: return "Invalid Type.";
                case 4096: return "Method Parameters Checked - Job Started.";
                case 32768: return "Failed.";
                case 32769: return "Access Denied.";
                case 32770: return "Not Supported.";
                case 32771: return "Status is Unknown.";
                case 32772: return "Timeout.";
                case 32773: return "Invalid Parameter.";
                case 32774: return "System is In Use.";
                case 32775: return "Invalid State for this Operation.";
                case 32776: return "Incorrect Data Type.";
                case 32777: return "System is Not Available.";
                case 32778: return "Out of Memory.";
                default: return "The Method Failed. The Reason is Unknown.";
            }
        }

        private static bool IsJobComplete(object jobStateObj)
        {
            JobState jobState = (JobState)(ushort)jobStateObj;

            return
                (jobState == JobState.Completed) ||
                (jobState == JobState.CompletedWithWarnings) ||
                (jobState == JobState.Terminated) ||
                (jobState == JobState.Exception) ||
                (jobState == JobState.Killed);
        }

        private static bool IsJobSuccessful(object jobStateObj)
        {
            JobState jobState = (JobState)(ushort)jobStateObj;

            return
                (jobState == JobState.Completed) ||
                (jobState == JobState.CompletedWithWarnings);
        }

    }

    file enum JobState
    {
        New = 2,
        Starting = 3,
        Running = 4,
        Suspended = 5,
        ShuttingDown = 6,
        Completed = 7,
        Terminated = 8,
        Killed = 9,
        Exception = 10,
        CompletedWithWarnings = 32768
    }

    file enum RequestedState : ushort
    {
        Other = 1,
        Enabled = 2,
        Disabled = 3,
        ShutDown = 4,
        Offline = 6,
        Test = 7,
        Defer = 8,
        Quiesce = 9,
        Reboot = 10,
        Reset = 11,
        Saving = 32773,
        Pausing = 32776,
        Resuming = 32777,
        FastSaved = 32779,
        FastSaving = 32780,
        RunningCritical = 32781,
        OffCritical = 32782,
        StoppingCritical = 32783,
        SavedCritical = 32784,
        PausedCritical = 32785,
        StartingCritical = 32786,
        ResetCritical = 32787,
        SavingCritical = 32788,
        PausingCritical = 32789,
        ResumingCritical = 32790,
        FastSavedCritical = 32791,
        FastSavingCritical = 32792
    }

    file enum VirtualMachineState : UInt16
    {
        ///<summary>The state of the virtual machine could not be determined.</summary>
        Unknown = 0,

        ///<summary>The virtual machine is in an other state.</summary>
        Other = 1,

        ///<summary>The virtual machine is running.</summary>
        Running = 2,

        ///<summary>The virtual machine is turned off.</summary>
        Off = 3,

        ///<summary>The virtual machine is in the process of turning off.</summary>
        ShuttingDown = 4,

        ///<summary>The virtual machine does not support being started or turned off.</summary>
        NotApplicable = 5,

        ///<summary>The virtual machine might be completing commands, and it will drop any new requests.</summary>
        EnabledButOffline = 6,

        ///<summary>The virtual machine is in a test state.</summary>
        InTest = 7,

        ///<summary>The virtual machine might be completing commands, but it will queue any new requests.</summary>
        Deferred = 8,

        ///<summary>The virtual machine is running but in a restricted mode. The behavior of the virtual machine is similar to the Running state, but it processes only a restricted set of commands. All other requests are queued.</summary>
        Quiesce = 9,

        ///<summary>The virtual machine is in the process of starting. New requests are queued.</summary>
        Starting = 10

    }

    file static class WmiHelpers
    {
        internal static void Dispose(this ManagementObject[] array)
        {
            foreach (ManagementObject managementObject in array)
                managementObject.Dispose();
        }

        internal static ManagementObject? First(this ManagementObjectCollection collection)
        {
            foreach (ManagementObject managementObject in collection)
                return managementObject;

            return null;
        }

        internal static ManagementObject[] ToObjectArray(this string[] managementStrings)
        {
            ManagementObject[] managementObjects = new ManagementObject[managementStrings.Length];
            for (int index = 0; index < managementStrings.Length; index++)
                managementObjects[index] = new ManagementObject(managementStrings[index]);
            return managementObjects;
        }

        internal static string[] ToStringArray(this ManagementObject[] managementObjects)
        {
            string[] managementStrings = new string[managementObjects.Length];
            for (int index = 0; index < managementObjects.Length; index++)
                managementStrings[index] = managementObjects[index].GetText(TextFormat.WmiDtd20);
            return managementStrings;
        }
    }
    #endregion
}
