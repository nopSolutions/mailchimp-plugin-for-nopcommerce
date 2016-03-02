using System;
using System.Web;
using System.Web.Mvc;
using Nop.Core;
using Nop.Services.Messages;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Misc.MailChimp.Controllers
{
    public class WebHooksController : BasePluginController
    {
        private readonly MailChimpSettings _settings;
        private readonly HttpContextBase _httpContext;
        private readonly IStoreContext _storeContext;
        private readonly INewsLetterSubscriptionService _newsLetterSubscriptionService;

        public WebHooksController(MailChimpSettings settings, HttpContextBase httpContext,
            IStoreContext storeContext, INewsLetterSubscriptionService newsLetterSubscriptionService)
        {
            _settings = settings;
            _httpContext = httpContext;
            _storeContext = storeContext;
            _newsLetterSubscriptionService = newsLetterSubscriptionService;
        }

        public ActionResult Index(string webHookKey)
        {
            if (String.IsNullOrWhiteSpace(_settings.WebHookKey))
                return Content("Invalid Request.");
            if (!string.Equals(_settings.WebHookKey, webHookKey, StringComparison.InvariantCultureIgnoreCase))
                return Content("Invalid Request.");

            if (IsUnsubscribe())
            {
                var subscription = _newsLetterSubscriptionService.GetNewsLetterSubscriptionByEmailAndStoreId(FindEmail(), _storeContext.CurrentStore.Id);

                if (subscription != null)
                {
                    // Do not publish unsubscribe event. Or duplicate events will occur.
                    _newsLetterSubscriptionService.DeleteNewsLetterSubscription(subscription, false);
                    return Content("OK");
                }
            }
            return Content("Invalid Request.");
        }

        /// <summary>
        /// Finds the email.
        /// </summary>
        /// <returns></returns>
        private string FindEmail()
        {
            const string KEY_NAME = "data[email]";
            return _httpContext.Request.Form[KEY_NAME];
        }

        /// <summary>
        /// Determines whether this instance is unsubscribe.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if this instance is unsubscribe; otherwise, <c>false</c>.
        /// </returns>
        private bool IsUnsubscribe()
        {
            const string KEY_NAME = "type";
            const string VALUE = "unsubscribe";

            return string.Equals(_httpContext.Request.Form[KEY_NAME], VALUE, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}