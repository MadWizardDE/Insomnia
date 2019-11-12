using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Lifetime;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;

namespace MadWizard.Insomnia.Service.SleepWatch
{
    class SleepLogWriter : LogFileSweeper.ISweepable, IPowerEventHandler, IDisposable
    {
        SleepWatchConfig _config;

        SleepMonitor _monitor;

        DirectoryInfo _logsDir;
        FileInfo _logFile;

        public SleepLogWriter(InsomniaConfig config, Lazy<SleepMonitor> monitor)
        {
            _config = config.SleepWatch;

            if (_config?.Log ?? false)
            {
                _monitor = monitor.Value;
                _monitor.PowerNap += OnPowerNap;
                _monitor.SleepOver += OnSleepOver;

                _logsDir = new DirectoryInfo("logs");
                _logFile = new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), "sleep.log"));

                Directory.CreateDirectory(_logsDir.FullName);
            }
        }

        [Autowired]
        ILogger<SleepLogWriter> Logger { get; set; }

        public DirectoryInfo LogsDir => _logsDir;

        DirectoryInfo LogFileSweeper.ISweepable.WatchDirectory => _logsDir;


        void IPowerEventHandler.OnPowerEvent(PowerBroadcastStatus status)
        {
            if (_config?.Log ?? false)
            {
                switch (status)
                {
                    case PowerBroadcastStatus.Suspend:
                        WriteLog(string.Empty); // ggfs. Tagesabschluss schreiben
                        break;
                }
            }
        }

        void IDisposable.Dispose()
        {
            if (_config?.Log ?? false)
            {
                TimeSpan sleepDuration = _monitor.Duration;

                var sb = new StringBuilder().AppendLine().Append("Shutdown.");

                if (sleepDuration.TotalMinutes > 0)
                {
                    sb.Append(" Total sleep duration: ");
                    sb.Append(FormatTimeSpan(sleepDuration));
                }

                WriteLog(sb.ToString());
            }
        }

        #region SleepMonitor
        private void OnSleepOver(object sender, EventArgs e)
        {
            FinishLog();
        }
        private void OnPowerNap(object sender, PowerNapEventArgs e)
        {
            _logFile.Refresh();

            if (_logFile.Length > 0)
            {
                var sb = new StringBuilder("zzzZZZzzz... ");
                sb.Append("(");
                sb.Append(FormatTimeSpan(e.SleepTime));
                sb.Append(")");
                sb.AppendLine();

                WriteLog(sb.ToString());
            }
        }
        #endregion

        #region ActivityDetector
        public void PrepareLog()
        {
            StringBuilder sb = new StringBuilder();

            if (_logFile.Exists)
            {
                if (DateTime.Now.Date != _logFile.LastWriteTime.Date)
                    ArchiveLog();
                else
                    sb.AppendLine();
            }

            sb.AppendLine("Startup.");
            sb.AppendLine();

            WriteLog(sb.ToString());
        }
        public void WriteActivity(ActivityAnalysis analysis)
        {
            var sb = new StringBuilder(DateTime.Now.ToString("HH:mm")).Append("\t");

            if (analysis.Tokens.Count() > 0)
                sb.Append(string.Join(", ", analysis.Tokens));
            else
                sb.Append("-");

            if (analysis.Error)
                sb.Append(" -> ERROR");

            sb.AppendLine();

            WriteLog(sb.ToString());
        }
        #endregion

        private void WriteLog(string text)
        {
            _logFile.Refresh();

            bool create = !_logFile.Exists;

            if (!create)
            {
                if (_logFile.LastWriteTime.Day != DateTime.Now.Day)
                {
                    FinishLog();

                    create = true;
                }
            }

            File.AppendAllText(_logFile.FullName, text);

            if (create)
            {
                _logFile.CreationTime = DateTime.Now;
            }
        }
        private void FinishLog()
        {
            var sb = new StringBuilder().AppendLine();
            sb.Append("Midnight! Total sleep duration: ");
            sb.Append(FormatTimeSpan(_monitor.Duration));
            File.AppendAllText(_logFile.FullName, sb.ToString());

            ArchiveLog();

            _monitor.ResetTime();
        }
        private void ArchiveLog()
        {
            _logFile.Refresh();

            string logs = _logsDir.FullName;
            string name = _logFile.CreationTime.ToString("yyyy-MM-dd") + ".log";
            string path = Path.Combine(logs, name);

            // Manchmal bleibt die Datei hängen.
            if (File.Exists(path))
            {
                File.Delete(path);

                Logger.LogWarning("Archive-File overwritten");
            }

            File.Move(_logFile.FullName, path);
        }

        static string FormatTimeSpan(TimeSpan time)
        {
            var sb = new StringBuilder();
            if (time.Days > 0)
                sb.Append(time.ToString("%d")).Append(" day(s), ");
            sb.Append(time.ToString(@"hh\:mm")).Append(" h");
            return sb.ToString();
        }
    }
}