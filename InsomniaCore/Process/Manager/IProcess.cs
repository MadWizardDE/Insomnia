using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Processes.Manager
{
    public interface IProcess
    {
        int Id { get; }
        int SessionId { get; }

        string Name { get; }

        Process NativeProcess { get; }

        IProcess? Parent { get; }
        bool HasParent(IProcess parent)
        {
            IProcess process = this;

            while (process.Parent != null)
            {
                if (process.Parent == parent)
                    return true;

                process = process.Parent;
            }

            return false;
        }

        Task Stop(TimeSpan? timeout = null);
    }
}
