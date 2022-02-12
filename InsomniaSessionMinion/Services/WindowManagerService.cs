using MadWizard.Insomnia.Service;
using MadWizard.Insomnia.Service.Sessions;
using ManagedWinapi.Windows;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Minion.Services
{
    class WindowManagerService : IWindowManagerService
    {
        [Autowired]
        ILogger<WindowManagerService> Logger { get; set; }

        public Task CloseWindow(string title)
        {
            bool WindowTitleFilter(SystemWindow window)
            {
                return window.Title.Contains(title);
            }

            foreach (SystemWindow win in SystemWindow.FilterToplevelWindows(WindowTitleFilter))
                win.SendClose();

            return Task.CompletedTask;
        }

        public async Task TerminateProcess(string processName, TimeSpan? maxRunningTime = null, TimeSpan? killTimeout = null, bool restart = false)
        {
            string fileName = null;

            foreach (Process process in Process.GetProcessesByName(processName))
            {
                if (process.SessionId != Process.GetCurrentProcess().SessionId)
                    continue;

                Logger.LogDebug($"Found process '{processName}' with PID {process.Id}.");

                fileName ??= process.MainModule.FileName;

                if (!maxRunningTime.HasValue || (DateTime.Now - process.StartTime) > maxRunningTime.Value)
                {
                    Logger.LogInformation($"Attempting to shutdown '{processName}'...");

                    // try gracefull shutdown
                    process.CloseMainWindow();

                    if (killTimeout.HasValue && killTimeout.Value.TotalMilliseconds > 0)
                        try
                        {
                            Logger.LogInformation($"Waiting for '{processName}' to exit...");

                            await process.WaitForExitAsync(new CancellationTokenSource((int)killTimeout.Value.TotalMilliseconds).Token);
                        }
                        catch (TimeoutException e)
                        {
                            Console.WriteLine(e);
                        }

                    if (!process.HasExited)
                    {
                        Logger.LogInformation($"Terminating '{processName}'.");

                        process.Kill(); // kill it anyway
                    }
                }
            }

            await Task.Delay(1000);

            if (restart)
            {
                if (Process.GetProcessesByName(processName).Length == 0)
                {
                    Logger.LogInformation($"Restarting '{processName}' ({fileName})...");

                    Process process = new();
                    process.StartInfo.FileName = fileName;
                    process.Start();
                }
                else
                    Logger.LogInformation($"Process '{processName}' is running already.");
            }
        }
    }
}