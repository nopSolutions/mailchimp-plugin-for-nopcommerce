using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.MailChimp
{
    public class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            routeBuilder.MapRoute("Plugin.Misc.MailChimp.Webhook", "Plugins/MailChimp/Webhook", new { controller = "MailChimp", action = "WebHook" });
        }

        public int Priority
        {
            get { return 0; }
        }

    }
}