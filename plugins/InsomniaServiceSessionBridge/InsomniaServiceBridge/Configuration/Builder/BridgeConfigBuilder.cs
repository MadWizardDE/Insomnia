using MadWizard.Insomnia.Service.Configuration;
using MadWizard.Insomnia.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MadWizard.Insomnia.Service.Duo.Configuration.Builder
{
    internal class BridgeConfigBuilder : IInitialConfigurationBuilder
    {
        public void Apply(InitializationPrefs ini, XDocument config)
        {
            if (ini["SessionMonitor"]["allowSleepControl"] is string control)
            {
                XElement monitor = GetOrCreateElement(config.Root!, "SessionMonitor");

                monitor.Add(new XAttribute("spawnMinions", "true"));

                XElement? session = null;

                switch (control)
                {
                    case "user":
                        session = GetOrCreateElement(monitor, "User");
                        if (!session.HasAttributes)
                            session.Add(new XAttribute("name", Environment.UserName));
                        break;

                    case "everyone":
                        session = GetOrCreateElement(monitor, "Everyone");
                        break;

                    case "administrator":
                        session = GetOrCreateElement(monitor, "Administrator");
                        break;
                }

                session?.Add(new XAttribute("allowControlSleep", "true"));
            }
        }

        private static XElement GetOrCreateElement(XElement parent, string name)
        {
            XElement? element = parent.Element(name);

            if (element == null)
            {
                parent.Add(element = new XElement(name));
            }

            return element;
        }
    }
}
