using Autofac;
using MadWizard.Insomnia.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace MadWizard.Insomnia.Service.Configuration
{
    public interface IInitialConfigurationBuilder
    {
        void Apply(InitializationPrefs ini, XDocument config);
    }

    internal class InitialConfigurationBuilder(string iniFilePath, string configFilePath) : BackgroundService
    {
        private const bool DELETE_INI_AFTER_SETUP = true;

        public required ILogger<InitialConfigurationBuilder> Logger { get; set; }

        public required IHostApplicationLifetime Lifetime { private get; init; }

        public required IEnumerable<IInitialConfigurationBuilder> Builders { private get; init; }

        private readonly InitializationPrefs Prefs = new(iniFilePath);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logger.LogInformation("Creating initial configuration...");

            try
            {
                XDocument document;
                if (File.Exists(configFilePath))
                {
                    using var stream = new StreamReader(configFilePath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                    document = XDocument.Load(stream);
                }
                else
                {
                    document = new XDocument(new XDeclaration("1.0", "UTF-8", null),
                        new XElement("InsomniaConfig", new XAttribute("version", InsomniaConfig.VERSION)));
                }

                XElement root = document.Root!;

                if (Prefs["SystemMonitor"]["timeout"] is string timeout)
                    root.SetAttributeValue("timeout", timeout);
                else root.Attribute("timeout")?.Remove();

                if (Prefs["SystemMonitor"]["idle"] is string idle)
                    root.SetAttributeValue("onIdle", idle);
                else root.Attribute("onIdle")?.Remove();

                if (Prefs["SystemMonitor"]["usage"] is string usage)
                    root.SetAttributeValue("onUsage", usage);
                else root.Attribute("onUsage")?.Remove();

                foreach (var builder in Builders)
                {
                    builder.Apply(Prefs, document);
                }

                using var writer = new StreamWriter(configFilePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                document.Save(writer);

                if (DELETE_INI_AFTER_SETUP)
                {
                    File.Delete(iniFilePath);
                }
            }
            finally
            {
                Lifetime.StopApplication();
            }
        }

    }
}
