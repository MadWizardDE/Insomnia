using MadWizard.Insomnia.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia
{
    internal class ActionHandler(MethodInfo method)
    {
        public string Name => method.GetCustomAttribute<ActionHandlerAttribute>()!.Name;

        public void InvokeWithContext(Actor actor, Arguments? arguments, params object[] context)
        {
            var paramters = new object?[method.GetParameters().Length];

            var argsIndex = 0;
            for (int i = 0; i < paramters.Length; i++) 
            {
                var paramter = method.GetParameters()[i];

                var value = context.Where(obj => paramter.ParameterType.IsAssignableFrom(obj.GetType())).FirstOrDefault();

                if (value == null && arguments != null && arguments.Length > argsIndex)
                {
                    value = arguments[argsIndex++];
                }

                if (value == null && paramter.HasDefaultValue)
                {
                    value = paramter.DefaultValue;
                }

                paramters[i] = value;
            } 

            method.Invoke(actor, paramters);
        }
    }
}
