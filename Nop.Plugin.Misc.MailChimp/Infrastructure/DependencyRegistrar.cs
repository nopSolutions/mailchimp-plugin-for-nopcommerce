using Autofac;
using Autofac.Core;
using Nop.Core.Configuration;
using Nop.Core.Data;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;
using Nop.Plugin.Misc.MailChimp.Data;
using Nop.Plugin.Misc.MailChimp.Domain;
using Nop.Plugin.Misc.MailChimp.Services;
using Nop.Data;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Misc.MailChimp.Infrastructure 
{
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
            builder.RegisterType<MailChimpManager>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<SynchronizationRecordService>().As<ISynchronizationRecordService>().InstancePerLifetimeScope();

            //data context
            this.RegisterPluginDataContext<MailChimpObjectContext>(builder, "nop_object_context_misc_mailchimp");

            //override required repository with our custom context
            builder.RegisterType<EfRepository<MailChimpSynchronizationRecord>>()
                .As<IRepository<MailChimpSynchronizationRecord>>()
                .WithParameter(ResolvedParameter.ForNamed<IDbContext>("nop_object_context_misc_mailchimp"))
                .InstancePerLifetimeScope();
        }

        /// <summary>
        /// Gets the order of this dependency registrar implementation
        /// </summary>
        public int Order
        {
            get { return 1; }
        }
    }
}