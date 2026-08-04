using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.MailChimp.Infrastructure;

/// <summary>
/// Represents a plugin route provider
/// </summary>
public class RouteProvider : IRouteProvider
{
    /// <summary>
    /// Register routes
    /// </summary>
    /// <param name="endpointRouteBuilder">Route builder</param>
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapControllerRoute(name: MailChimpDefaults.Route.Configuration,
            pattern: "Admin/MailChimp/Configure",
            defaults: new { controller = "MailChimp", action = "Configure", area = AreaNames.ADMIN });

        //webhook routes
        endpointRouteBuilder.MapControllerRoute(MailChimpDefaults.Route.BatchWebhookRoute,
            "Plugins/MailChimp/BatchWebhook",
            new { controller = "MailChimpWebhook", action = "BatchWebhook" });

        endpointRouteBuilder.MapControllerRoute(MailChimpDefaults.Route.WebhookRoute,
            "Plugins/MailChimp/Webhook",
            new { controller = "MailChimpWebhook", action = "WebHook" });
    }

    /// <summary>
    /// Gets a priority of route provider
    /// </summary>
    public int Priority => 0;

}