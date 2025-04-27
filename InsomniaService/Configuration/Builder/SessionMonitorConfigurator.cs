using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MadWizard.Insomnia.Service.Configuration.Builder
{
    internal class SessionMonitorConfigurator : IInitialConfigurationBuilder
    {
        void IInitialConfigurationBuilder.Apply(InitializationPrefs ini, XDocument config)
        {
            // no safe auto config possible
            if (config.Root!.Elements("SessionMonitor").Any())
                return;

            if (ini["SessionMonitor"]["track"] is string track)
            {
                var monitor = new XElement("SessionMonitor");

                switch (track)
                {
                    case "user":
                        monitor.Add(new XElement("User", new XAttribute("name", Environment.UserName)));
                        break;

                    case "everyone":
                        monitor.Add(new XElement("Everyone"));
                        break;

                    case "administrator":
                        monitor.Add(new XElement("Administrator"));
                        break;
                }

                config.Root?.Add(monitor);
            }
        }
    }
}
