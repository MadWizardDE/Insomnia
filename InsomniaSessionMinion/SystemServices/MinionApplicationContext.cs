using Autofac;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MadWizard.Insomnia.Minion
{
    class MinionApplicationContext : ApplicationContext, IUserInterface, IDisposable
    {
        SynchronizationContext _syncContext;

        public MinionApplicationContext()
        {
            StartUIThread();
        }

        private void StartUIThread()
        {
            using (ManualResetEvent wait = new ManualResetEvent(false))
            {
                void FinishStartup(object sender, EventArgs e)
                {
                    Application.Idle -= FinishStartup;

                    _syncContext = SynchronizationContext.Current;

                    wait.Set();
                }

                Thread thread = new Thread(() =>
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Idle += FinishStartup;
                    Application.Run(this);
                });

                thread.Name = GetType().Name;
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                wait.WaitOne();
            }
        }

        #region Control-Flow
        void IUserInterface.SendAction(Action action)
        {
            if (_syncContext == null)
                throw new InvalidOperationException("No SynchronizationContext");

            _syncContext.Send(delegate { action(); }, null);
        }
        Task IUserInterface.SendActionAsync(Action action)
        {
            var taskSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _syncContext.Post(delegate
            {
                try
                {
                    action();

                    taskSource.SetResult(true);
                }
                catch (Exception e)
                {
                    taskSource.SetException(e);
                }
            }, null);

            return taskSource.Task;
        }

        void IUserInterface.PostAction(Action action)
        {
            if (_syncContext == null)
                throw new InvalidOperationException("No SynchronizationContext");

            _syncContext.Post(delegate { action(); }, null);
        }
        #endregion

    }
}