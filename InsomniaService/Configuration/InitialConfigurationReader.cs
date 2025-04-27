using Autofac;
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
    public interface IInitialConfigurationReader
    {
        void Apply(XDocument config, InitializationPrefs ini);
    }

    internal class InitialConfigurationReader(string configFilePath, string iniFilePath) : BackgroundService
    {
        public required ILogger<InitialConfigurationReader> Logger { get; set; }

        public required IHostApplicationLifetime Lifetime { private get; init; }

        public required IEnumerable<IInitialConfigurationReader> Readers { private get; init; }

        private readonly InitializationPrefs Prefs = new(iniFilePath);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Logger.LogInformation("Reading initial configuration...");

            try
            {
                using var stream = new StreamReader(configFilePath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                var document = XDocument.Load(stream);

                Prefs["SystemMonitor"]["timeout"] = document.Root?.Attribute("timeout")?.Value;
                Prefs["SystemMonitor"]["idle"] = document.Root?.Attribute("onIdle")?.Value;
                Prefs["SystemMonitor"]["usage"] = document.Root?.Attribute("onUsage")?.Value;

                foreach (var reader in Readers)
                {
                    reader.Apply(document, Prefs);
                }
            }
            finally
            {
                Lifetime.StopApplication();
            }
        }
    }
}
