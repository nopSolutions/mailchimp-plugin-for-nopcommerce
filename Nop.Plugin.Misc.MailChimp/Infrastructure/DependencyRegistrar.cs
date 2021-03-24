using Microsoft.Extensions.DependencyInjection;
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
        /// <param name="services">Collection of service descriptors</param>
        /// <param name="typeFinder">Type finder</param>
        /// <param name="appSettings">App settings</param>
        public void Register(IServiceCollection services, ITypeFinder typeFinder, AppSettings appSettings)
        {
            //register MailChimp manager
            services.AddScoped<MailChimpManager>();

            //register custom data services
            services.AddScoped<ISynchronizationRecordService, SynchronizationRecordService>();
        }

        /// <summary>
        /// Gets the order of this dependency registrar implementation
        /// </summary>
        public int Order => 1;
    }
}