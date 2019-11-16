using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service.Sessions
{
    public interface ISessionControlService
    {
        public Task Lock();

        public Task Logoff(bool force = false);
    }
}