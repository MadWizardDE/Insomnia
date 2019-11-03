using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service.Sessions
{
    public interface INotificationAreaService
    {
        public const int DEFAULT_TIMEOUT = 10000;

        public bool IsMoonriseCommanderEnabled { get; set; }

        public Task ShowNotificationAsync(NotifyMessageType type, string title, string text, int timeout = DEFAULT_TIMEOUT);

        public Task ShowWakeTarget(WakeTarget target);
        public Task HideWakeTarget(WakeTarget target);

        public Task ShowWakeOption(WakeOption option);
        public Task HideWakeOption(WakeOption option);

        [Serializable]
        public struct WakeTarget
        {
            public WakeTarget(string name, string nameNetwork = null, NetworkType typeNetwork = NetworkType.Unknown)
            {
                Name = name;
                NetworkName = nameNetwork;
                NetworkType = typeNetwork;

                AvailableModes = null;
                SelectedMode = null;
            }

            public string Name { get; set; }
            public string NetworkName { get; set; }
            public NetworkType NetworkType { get; set; }

            public object[] AvailableModes { get; set; }
            public object SelectedMode { get; set; }
        }

        [Serializable]
        public struct WakeOption
        {
            public const string RESOLVE_IP = "IP_RESOLVE";

            public WakeOption(string name, object value)
            {
                Key = name;
                Value = value;
            }

            public string Key { get; private set; }
            public object Value { get; private set; }
        }

        [Serializable]
        public enum NetworkType
        {
            Unknown = 0,

            Wired,
            Wireless,
            Remote
        }

        [Serializable]
        public enum NotifyMessageType
        {
            None,
            Info,
            Warning,
            Error
        }
    }
}