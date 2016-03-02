using Autofac;
using Autofac.Core;
using Autofac.Integration.Mvc;
using Nop.Core.Configuration;
using Nop.Core.Data;
using Nop.Core.Infrastructure;
using Nop.Core.Infrastructure.DependencyManagement;
using Nop.Data;
using Nop.Plugin.Misc.MailChimp.Data;
using Nop.Plugin.Misc.MailChimp.Services;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Misc.MailChimp 
{
    public class DependencyRegistrar : IDependencyRegistrar
    {

        private const string CONTEXT_DEPENDENCY_REGISTRY_KEY = "Nop.Plugin.Misc.MailChimp-ObjectContext";

        /// <summary>
        /// Registers the specified builder.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="typeFinder">The type finder.</param>
        /// <param name="config"></param>
        public void Register(ContainerBuilder builder, ITypeFinder typeFinder, NopConfig config)
        {
            builder.RegisterType<SubscriptionEventQueueingService>().As<ISubscriptionEventQueueingService>().InstancePerLifetimeScope();
            builder.RegisterType<MailChimpInstallationService>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<MailChimpApiService>().As<IMailChimpApiService>().InstancePerLifetimeScope();

            //data context
            this.RegisterPluginDataContext<MailChimpObjectContext>(builder, CONTEXT_DEPENDENCY_REGISTRY_KEY);

            //override required repository with our custom context
            builder.RegisterType<EfRepository<MailChimpEventQueueRecord>>()
                .As<IRepository<MailChimpEventQueueRecord>>()
                .WithParameter(ResolvedParameter.ForNamed<IDbContext>(CONTEXT_DEPENDENCY_REGISTRY_KEY))
                .InstancePerLifetimeScope();
        }

        /// <summary>
        /// Registers the object context.
        /// </summary>
        /// <param name="builder">The builder.</param>
        private void RegisterObjectContext(ContainerBuilder builder)
        {
            //Open the data settings manager
            var dataSettingsManager = new DataSettingsManager();
            var dataProviderSettings = dataSettingsManager.LoadSettings();

            string nameOrConnectionString = null;

            if (dataProviderSettings != null && dataProviderSettings.IsValid())
            {
                //determine if the connection string exists
                nameOrConnectionString = dataProviderSettings.DataConnectionString;
            }

            //Register the named instance
            builder.Register<IDbContext>(c => new MailChimpObjectContext(nameOrConnectionString ?? c.Resolve<DataSettings>().DataConnectionString))
                .Named<IDbContext>(CONTEXT_DEPENDENCY_REGISTRY_KEY).InstancePerLifetimeScope();

            //Register the type
            builder.Register(c => new MailChimpObjectContext(nameOrConnectionString ?? c.Resolve<DataSettings>().DataConnectionString)).InstancePerLifetimeScope();
        }

        /// <summary>
        /// Gets the order.
        /// </summary>
        public int Order
        {
            get { return 0; }
        }
    }
}