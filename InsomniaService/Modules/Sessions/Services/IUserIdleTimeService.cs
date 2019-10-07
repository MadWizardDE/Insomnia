using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.Sessions
{
    public interface IUserIdleTimeService
    {
        long IdleTime { get; }
    }
}