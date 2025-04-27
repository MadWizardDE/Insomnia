using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia
{
    public interface IInspectable
    {
        public IEnumerable<UsageToken> Inspect(TimeSpan interval);
    }
}
