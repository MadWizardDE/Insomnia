using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Network
{
    public interface IVirtualHostManager
    {
        IVirtualHost? FindHostByName(string name);
    }
}
