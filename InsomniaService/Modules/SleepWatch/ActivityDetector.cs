using Autofac;
using MadWizard.Insomnia.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

using MadWizard.Insomnia.Tools;
using MadWizard.Insomnia.Service.Lifetime;

using static MadWizard.Insomnia.Configuration.SleepWatchConfig;
using static MadWizard.Insomnia.Tools.Win32API;

namespace MadWizard.Insomnia.Service.SleepWatch
{
    class ActivityDetector : IStartable, IPowerEventHandler, IDisposable
    {
        const int INITAL_DELAY = 5000;

        SleepLogWriter _sleepLogWriter;

        List<IDetector> _detectors;
        List<IExaminer> _examiners;

        Timer _analysisTimer;

        public ActivityDetector(InsomniaConfig config,
            IEnumerable<Lazy<IDetector>> lazyDetectors, IEnumerable<Lazy<IExaminer>> lazyExaminers,
            SleepLogWriter sleepLogWriter = null)
        {
            if (config.SleepWatch?.ActivityDetector != null)
            {
                _detectors = lazyDetectors.Select(l => l.Value).ToList();
                _examiners = lazyExaminers.Select(l => l.Value).ToList();

                _sleepLogWriter = sleepLogWriter;

                _analysisTimer = new Timer();
                _analysisTimer.Interval = config.Interval;
                _analysisTimer.Elapsed += OnTimerElapsed;
            }
        }

        [Autowired]
        ILogger<ActivityDetector> Logger { get; set; }

        internal int IdleCount { get; private set; }

        void IStartable.Start()
        {
            _sleepLogWriter?.PrepareLog();

            OnTimerStart();
        }
        void IPowerEventHandler.OnPowerEvent(PowerBroadcastStatus status)
        {
            switch (status)
            {
                case PowerBroadcastStatus.Suspend:
                    OnTimerStop();
                    break;

                case PowerBroadcastStatus.ResumeSuspend:
                    OnTimerStart();
                    break;
            }
        }
        void IDisposable.Dispose()
        {
            OnTimerStop();
        }

        private void OnTimerStart()
        {
            if (_analysisTimer != null)
            {
                Task.Run(() =>
                {
                    System.Threading.Thread.Sleep(INITAL_DELAY);

                    OnTimerElapsed(_analysisTimer, null);

                    _analysisTimer.Start();
                });
            }
        }
        private void OnTimerElapsed(object sender, ElapsedEventArgs args)
        {
            try
            {
                bool idle = true, error = false;
                List<string> activityTokens = new List<string>();
                foreach (IDetector detector in _detectors)
                {
                    try
                    {
                        var (tokens, busy) = detector.Scan();

                        foreach (string token in tokens)
                            if (!activityTokens.Contains(token))
                                activityTokens.Add(token);

                        if (busy)
                            idle = false;
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e, "Activity-Scan failed");

                        error = true;
                    }
                }

                ActivityAnalysis analysis = new ActivityAnalysis(activityTokens, idle, IdleCount = idle ? IdleCount + 1 : 0)
                {
                    Error = error
                };

                foreach (IExaminer examiner in _examiners)
                    try
                    {
                        examiner.Examine(analysis);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e, "Activity-Examination failed");

                        analysis.Error = true;
                    }

                _sleepLogWriter?.WriteActivity(analysis);
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Activity-Detection failed");
            }
        }
        private void OnTimerStop()
        {
            _analysisTimer?.Stop();

            IdleCount = 0;
        }

        internal class SleepInhibitor : IExaminer, IDisposable
        {
            internal Win32API.PowerRequest Request { get; private set; }

            void IExaminer.Examine(ActivityAnalysis analysis)
            {
                if (analysis.Idle)
                {
                    Request?.Dispose();
                    Request = null;
                }
                else
                {
                    string tokens = string.Join(", ", analysis.Tokens.Where(t => !(t.StartsWith("((") && t.EndsWith("))"))));
                    string reason = $"Kein Standby-Modus wegen: {tokens}";

                    if (Request?.Reason != reason)
                    {
                        Request?.Dispose();
                        Request = new PowerRequest(reason);
                    }
                }
            }

            void IDisposable.Dispose()
            {
                Request?.Dispose();
                Request = null;
            }
        }
        internal class SleepEnforcer : IExaminer
        {
            int _idleMax;

            bool _hibernate;

            public SleepEnforcer(InsomniaConfig config, int idleMax)
            {
                _idleMax = idleMax;

                _hibernate = config.SleepWatch?.SuspendTo == SuspendState.HIBERNATE;
            }

            [Autowired]
            ILogger<ActivityDetector> Logger { get; set; }

            void IExaminer.Examine(ActivityAnalysis anlysis)
            {
                if (anlysis.IdleCount > _idleMax)
                {
                    Logger.LogInformation(InsomniaEventId.COMPUTER_IDLE, "Computer idle", 5);

                    Win32API.EnterStandby(_hibernate);
                }
            }
        }

        public interface IDetector
        {
            (string[] tokens, bool busy) Scan();
        }
        public interface IExaminer
        {
            void Examine(ActivityAnalysis analysis);
        }
    }

    public class ActivityAnalysis
    {
        internal ActivityAnalysis(IEnumerable<string> tokens, bool idle, int idleCount)
        {
            Tokens = tokens;

            Idle = idle;
            IdleCount = idleCount;
        }

        public IEnumerable<string> Tokens { get; private set; }

        public bool Idle { get; private set; }
        public bool Error { get; internal set; }

        public int IdleCount { get; private set; }
    }
}