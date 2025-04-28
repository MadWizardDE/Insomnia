using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MadWizard.Insomnia.Service.Configuration.Builder
{
    internal class SessionMonitorConfigurator : IInitialConfigurationBuilder, IInitialConfigurationReader
    {
        void IInitialConfigurationBuilder.Apply(InitializationPrefs ini, XDocument config)
        {
            if (ini["SessionMonitor"]["track"] is string track && track == "everyone")
            {
                if (config.Root!.Element("SessionMonitor") == null)
                {
                    config.Root!.Add(new XElement("SessionMonitor"));
                }
            }
            else if (config.Root!.Element("SessionMonitor") is XElement monitor)
            {
                monitor.Remove();
            }
        }

        void IInitialConfigurationReader.Apply(XDocument config, InitializationPrefs ini)
        {
            ini["SessionMonitor"]["track"] = config.Root!.Elements("SessionMonitor").Any() ? "everyone" : null;
        }
    }
}   
