using Autofac;
using Autofac.Core;
using MadWizard.Insomnia.Configuration;
using MadWizard.Insomnia.Service.Bridge.Configuration;
using MadWizard.Insomnia.Service.Bridge.Notification;
using MadWizard.Insomnia.Service.Duo.Configuration.Builder;
using MadWizard.Insomnia.Session.Manager;
using MadWizard.Insomnia.Session.Manager.Bridged;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace MadWizard.Insomnia.Service.Bridge
{
    public class PluginModule : Insomnia.PluginModule
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(ctx => ctx.Resolve<IConfiguration>().Get<BridgeConfig>(opt => opt.BindNonPublicProperties = true)!).AsSelf();

            builder.RegisterType<NotificationAreaController>()
                .AsImplementedInterfaces().AsSelf()
                .SingleInstance();

            builder.RegisterType<BridgedTerminalServicesManager>()
                .AsImplementedInterfaces().AsSelf()
                .SingleInstance();

            builder.RegisterType<InspectionController>()
                .AsImplementedInterfaces().AsSelf()
                .SingleInstance();
        }
    }

    public class ConfigPluginModule : Insomnia.Service.Configuration.ConfigPluginModule
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<BridgeConfigBuilder>()
                .AsImplementedInterfaces()
                .SingleInstance();
        }
    }

}
