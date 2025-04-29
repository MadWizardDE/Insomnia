using MadWizard.Insomnia.Service.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MadWizard.Insomnia.Service.Duo.Configuration.Builder
{
    internal class DuoConfigConfigurator : IInitialConfigurationBuilder, IInitialConfigurationReader
    {
        public void Apply(InitializationPrefs ini, XDocument config)
        {
            if (ini["DuoStreamMonitor"]["idle"] is string idle)
                MakeMonitor(config).SetAttributeValue("onInstanceIdle", idle);
            else MakeMonitor(config).Attribute("onInstanceIdle")?.Remove();

            if (ini["DuoStreamMonitor"]["demand"] is string demand)
                MakeMonitor(config).SetAttributeValue("onInstanceDemand", demand);
            else MakeMonitor(config).Attribute("onInstanceDemand")?.Remove();
        }

        private static XElement MakeMonitor(XDocument config)
        {
            if (config.Root?.Element("DuoStreamMonitor") is not XElement monitor)
            {
                monitor = new XElement("DuoStreamMonitor");

                monitor.Add(new XComment(" Add individual instance configuration here... "));

                config.Root?.Add(monitor);
            }

            return monitor;
        }

        void IInitialConfigurationReader.Apply(XDocument config, InitializationPrefs ini)
        {
            if (config.Root?.Element("DuoStreamMonitor") is XElement duo)
            {
                ini["DuoStreamMonitor"]["idle"] = duo.Attribute("onInstanceIdle")?.Value;
                ini["DuoStreamMonitor"]["demand"] = duo.Attribute("onInstanceDemand")?.Value;
            }
        }
    }
}