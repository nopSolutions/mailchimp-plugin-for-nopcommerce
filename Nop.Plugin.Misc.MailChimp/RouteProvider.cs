using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace Nop.Plugin.Misc.MailChimp
{
    public class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            routes.MapRoute("Plugin.Misc.MailChimp.Webhook",
                "Plugins/MailChimp/Webhook",
                new { controller = "MailChimp", action = "WebHook" },
                new[] { "Nop.Plugin.Misc.MailChimp.Controllers" });
        }

        public int Priority
        {
            get { return 0; }
        }

    }
}