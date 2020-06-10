using Autofac;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;
using Nop.Plugin.Misc.MailChimp.Services;

namespace Nop.Plugin.Misc.MailChimp.Infrastructure
{
    /// <summary>
    /// Represents a plugin dependency registrar
    /// </summary>
    public class DependencyRegistrar : IDependencyRegistrar
    {
        /// <summary>
        /// Register services and interfaces
        /// </summary>
        /// <param name="builder">Container builder</param>
        /// <param name="typeFinder">Type finder</param>
        /// <param name="config">Config</param>
        public virtual void Register(ContainerBuilder builder, ITypeFinder typeFinder, NopConfig config)
        {
            //register MailChimp manager
            builder.RegisterType<MailChimpManager>().AsSelf().InstancePerLifetimeScope();

            //register custom data services
            builder.RegisterType<SynchronizationRecordService>().As<ISynchronizationRecordService>().InstancePerLifetimeScope();
        }

        /// <summary>
        /// Gets the order of this dependency registrar implementation
        /// </summary>
        public int Order => 1;
    }
}