using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MadWizard.Insomnia.Service.Lifetime
{
    public static class InsomniaServiceLifetimeHostBuilderExtensions
    {
        /// <summary>
        /// Sets the host lifetime to WindowsServiceLifetime, sets the Content Root,
        /// and enables logging to the event log with the application name as the default source name.
        /// </summary>
        /// <remarks>
        /// This is context aware and will only activate if it detects the process is running
        /// as a Windows Service.
        /// </remarks>
        /// <param name="hostBuilder"></param>
        /// <returns></returns>
        public static IHostBuilder UseInsomniaServiceLifetime(this IHostBuilder hostBuilder)
        {
            if (IsWindowsService())
            {
                // Host.CreateDefaultBuilder uses CurrentDirectory for VS scenarios, but CurrentDirectory for services is c:\Windows\System32.
                //hostBuilder.UseContentRoot(AppContext.BaseDirectory);
                hostBuilder.UseContentRoot(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Locati‌​on));
                hostBuilder.ConfigureLogging((hostingContext, logging) =>
                {
                    logging.AddEventLog();
                });
                hostBuilder.ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<IHostLifetime, WindowsServiceLifetime>();
                    services.Configure<EventLogSettings>(settings =>
                    {
                        if (string.IsNullOrEmpty(settings.SourceName))
                        {
                            settings.SourceName = hostContext.HostingEnvironment.ApplicationName;
                        }
                    });
                });
            }

            return hostBuilder;
        }

        private static bool IsWindowsService()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return false;

            var parent = Process.GetCurrentProcess().GetParentProcess();
            if (parent == null)
                return false;

            return parent.SessionId == 0 && string.Equals("services", parent.ProcessName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
