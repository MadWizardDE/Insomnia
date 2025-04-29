using Autofac;
using MadWizard.Insomnia.Service.Configuration;
using MadWizard.Insomnia.Service.Duo.Configuration;
using MadWizard.Insomnia.Service.Duo.Configuration.Builder;
using MadWizard.Insomnia.Service.Duo.Manager;
using Microsoft.Extensions.Configuration;

namespace MadWizard.Insomnia.Service.Duo
{
    public class PluginModule : Insomnia.PluginModule
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(ctx => ctx.Resolve<IConfiguration>().Get<DuoConfig>(opt => opt.BindNonPublicProperties = true)!).AsSelf();

            builder.RegisterType<DuoManager>()
                .AsImplementedInterfaces().AsSelf()
                .SingleInstance();

            builder.RegisterType<DuoStreamMonitor>()
                .AsImplementedInterfaces().AsSelf()
                .SingleInstance();

            //builder.RegisterType<DuoStreamUX>()
            //    .AsImplementedInterfaces()
            //    .SingleInstance();
        }
    }

    public class ConfigPluginModule : Insomnia.Service.Configuration.ConfigPluginModule
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<DuoConfigConfigurator>()
                .AsImplementedInterfaces()
                .SingleInstance();
        }
    }
}
    