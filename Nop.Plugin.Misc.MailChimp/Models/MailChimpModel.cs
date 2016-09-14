using System.Collections.Generic;
using System.Web.Mvc;
using Nop.Web.Framework;

namespace Nop.Plugin.Misc.MailChimp.Models
{
    public class MailChimpModel
    {
        public MailChimpModel()
        {
            AvailableLists = new List<SelectListItem>();
        }

        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Misc.MailChimp.Fields.ApiKey")]
        public string ApiKey { get; set; }

        [NopResourceDisplayName("Plugins.Misc.MailChimp.AccountInfo")]
        public string AccountInfo { get; set; }

        [NopResourceDisplayName("Plugins.Misc.MailChimp.Fields.UseEcommerceApi")]
        public bool UseEcommerceApi { get; set; }

        [NopResourceDisplayName("Plugins.Misc.MailChimp.Fields.List")]
        public string ListId { get; set; }
        public bool ListId_OverrideForStore { get; set; }

        public IList<SelectListItem> AvailableLists { get; set; }

        [NopResourceDisplayName("Plugins.Misc.MailChimp.Fields.AutoSync")]
        public bool AutoSync { get; set; }

        [NopResourceDisplayName("Plugins.Misc.MailChimp.Fields.AutoSyncEachMinutes")]
        public int AutoSyncEachMinutes { get; set; }
    }
}