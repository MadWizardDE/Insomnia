using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
            var insomniaDirectory = new DirectoryInfo(Assembly.GetExecutingAssembly().Locati‌​on).Parent.Parent;

            Directory.SetCurrentDirectory(insomniaDirectory.FullName);
            hostBuilder.UseContentRoot(insomniaDirectory.FullName);

            if (IsWindowsService())
            {
                hostBuilder.ConfigureServices((ctx, services) => services.AddSingleton<IHostLifetime, InsomniaServiceLifetime>());
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
