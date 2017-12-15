using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.MailChimp.Infrastructure
{
    /// <summary>
    /// Represents a plugin route provider
    /// </summary>
    public class RouteProvider : IRouteProvider
    {
        /// <summary>
        /// Register routes
        /// </summary>
        /// <param name="routeBuilder">Route builder</param>
        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            //webhook routes
            routeBuilder.MapRoute(MailChimpDefaults.BatchWebhookRoute,
                "Plugins/MailChimp/BatchWebhook", new { controller = "MailChimp", action = "BatchWebhook" });

            routeBuilder.MapRoute(MailChimpDefaults.WebhookRoute,
                "Plugins/MailChimp/Webhook", new { controller = "MailChimp", action = "WebHook" });
        }

        /// <summary>
        /// Gets a priority of route provider
        /// </summary>
        public int Priority
        {
            get { return 0; }
        }

    }
}