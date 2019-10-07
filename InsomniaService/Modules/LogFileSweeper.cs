using Autofac;
using MadWizard.Insomnia.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MadWizard.Insomnia.Service
{
    class LogFileSweeper : IStartable, IDisposable
    {
        LogSweeperConfig _config;

        IEnumerable<ISweepable> _sweepables;

        List<FileSystemWatcher> _watchers;

        public LogFileSweeper(InsomniaConfig config, IEnumerable<ISweepable> sweepables)
        {
            _config = config.LogSweeper;

            _sweepables = sweepables;

            _watchers = new List<FileSystemWatcher>();
        }

        [Autowired]
        ILogger<LogFileSweeper> Logger { get; set; }

        public void Start()
        {
            foreach (ISweepable module in _sweepables)
            {
                DirectoryInfo directory = (module as ISweepable).WatchDirectory;

                if (directory?.Exists ?? false)
                {
                    AddWatcher(new FileSystemWatcher(directory.FullName));
                }
            }
        }

        private void SweepFileSystem(FileSystemInfo[] infos, int retainCount)
        {
            var entries = new List<FileSystemInfo>();
            foreach (FileSystemInfo info in infos)
            {
                info.Refresh();
                entries.Add(info);
            }

            if (entries.Count > retainCount)
            {
                entries.Sort((a, b) => b.LastWriteTime.CompareTo(a.LastWriteTime));

                foreach (FileSystemInfo info in entries.GetRange(retainCount, entries.Count - retainCount))
                {
                    if (info is DirectoryInfo)
                        ((DirectoryInfo)info).Delete(true);
                    else
                        info.Delete();
                }
            }
        }

        public void Dispose()
        {
            foreach (FileSystemWatcher watcher in _watchers.ToArray())
                RemoveWatcher(watcher);
        }

        #region FileSystemWatcher
        private void AddWatcher(FileSystemWatcher watcher)
        {
            watcher.Created += Watcher_Created;
            watcher.Error += Watcher_Error;
            watcher.EnableRaisingEvents = true;

            _watchers.Add(watcher);
        }
        private void Watcher_Created(object sender, FileSystemEventArgs e)
        {
            DirectoryInfo directory = new DirectoryInfo((sender as FileSystemWatcher).Path);

            FileSystemInfo[] infos = directory.GetFiles();
            if (infos.Length == 0)
                infos = directory.GetDirectories();
            SweepFileSystem(infos, _config.Count);
        }
        private void Watcher_Error(object sender, ErrorEventArgs e)
        {
            Logger.LogError(e.GetException(), "FileSystemWatcher Error");

            RemoveWatcher((FileSystemWatcher)sender);
        }
        private void RemoveWatcher(FileSystemWatcher watcher)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Error -= Watcher_Error;
            watcher.Created -= Watcher_Created;
            watcher.Dispose();

            _watchers.Remove(watcher);
        }
        #endregion

        public interface ISweepable
        {
            DirectoryInfo WatchDirectory { get; }
        }
    }
}