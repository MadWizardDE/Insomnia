using MadWizard.Insomnia.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using static MadWizard.Insomnia.Configuration.SleepWatchConfig.ActivityDetectorConfig;

namespace MadWizard.Insomnia.Service.SleepWatch.Detector
{
    class PowerRequestDetector : ActivityDetector.IDetector, ActivityDetector.IExaminer, LogFileSweeper.ISweepable
    {
        PowerRequestConfig _config;

        public PowerRequestDetector(InsomniaConfig config, SleepLogWriter logWriter = null)
        {
            _config = config.SleepWatch.ActivityDetector.PowerRequests;

            RequestsDir = new DirectoryInfo(Path.Combine(logWriter.LogsDir.FullName, "requests"));
        }

        [Autowired]
        ILogger<PowerRequestDetector> Logger { get; set; }

        public DirectoryInfo RequestsDir { get; private set; }

        #region Interface-Implementations
        DirectoryInfo LogFileSweeper.ISweepable.WatchDirectory => RequestsDir;

        (string[] tokens, bool busy) ActivityDetector.IDetector.Scan()
        {
            var tokenList = new HashSet<string>();

            string output = QueryPowerRequests();

            foreach (var requestInfo in _config.Request.Values)
                foreach (string keyWord in requestInfo.Strings)
                    if (output.IndexOf(keyWord, StringComparison.InvariantCultureIgnoreCase) >= 0)
                        tokenList.Add($"(({requestInfo.Name}))");

            return (tokenList.ToArray(), (_config?.KeepAwake ?? false) ? tokenList.Count > 0 : false);
        }

        void ActivityDetector.IExaminer.Examine(ActivityAnalysis analysis)
        {
            if (analysis.Idle && _config.LogIfIdle)

                try
                {
                    var now = DateTime.Now;
                    string name = now.ToString("HHmm") + ".log";
                    string today = Path.Combine(RequestsDir.FullName, now.ToString("yyyy-MM-dd"));
                    string path = Path.Combine(today, name);
                    Directory.CreateDirectory(today);
                    File.AppendAllText(path, QueryPowerRequests());
                }
                catch (Exception e)
                {
                    Logger.LogError(e, "Failed to save power requests.");
                }

        }
        #endregion

        #region Tools
        private static string QueryPowerRequests()
        {
            Process process = new Process();
            process.StartInfo.FileName = @"powercfg";
            process.StartInfo.Arguments = "-requests";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.Start();

            return process.StandardOutput.ReadToEnd();
        }
        #endregion
    }
}
