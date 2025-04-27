using MadWizard.Insomnia.Pipe.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace MadWizard.Insomnia.Service.Bridge.Notification
{
    public interface ITokenDescriptor<T> where T : UsageToken
    {
        public UsageTokenInfo DescribeToken(T token);
    }
}
