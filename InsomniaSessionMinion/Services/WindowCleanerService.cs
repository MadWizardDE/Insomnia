using MadWizard.Insomnia.Service.Sessions;
using ManagedWinapi.Windows;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Minion.Services
{
    class WindowCleanerService : IWindowCleanerService
    {
        public Task Wipe(string title)
        {
            bool WindowFilter(SystemWindow window)
            {
                return window.Title.Contains(title);
            }

            foreach (SystemWindow win in SystemWindow.FilterToplevelWindows(WindowFilter))
                win.SendClose();

            return Task.CompletedTask;
        }
    }
}