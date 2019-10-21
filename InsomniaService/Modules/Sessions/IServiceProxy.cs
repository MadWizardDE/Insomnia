using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.Sessions
{
    public interface IServiceProxy
    {
        IInvocationContext InvokeWithOptions(TimeSpan? timeout = null);
    }

    public interface IInvocationContext : IDisposable
    {
        TimeSpan Timeout { get; }
    }
}