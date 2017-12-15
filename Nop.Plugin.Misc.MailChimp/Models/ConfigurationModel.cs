using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Misc.MailChimp.Models
{
    /// <summary>
    /// Represents MailChimp configuration model
    /// </summary>
    public class ConfigurationModel
    {
        #region Ctor

        public ConfigurationModel()
        {
            AvailableLists = new List<SelectListItem>();
        }

        #endregion

        #region Properties

        public int ActiveStoreScopeConfiguration { get; set; }

        public bool SynchronizationStarted { get; set; }

        [NopResourceDisplayName("Plugins.Misc.MailChimp.Fields.ApiKey")]
        [DataType(DataType.Password)]
        [NoTrim]
        public string ApiKey { get; set; }

        [NopResourceDisplayName("Plugins.Misc.MailChimp.Fields.AccountInfo")]
        public string AccountInfo { get; set; }

        [NopResourceDisplayName("Plugins.Misc.MailChimp.Fields.PassEcommerceData")]
        public bool PassEcommerceData { get; set; }

        [NopResourceDisplayName("Plugins.Misc.MailChimp.Fields.List")]
        public string ListId { get; set; }
        public bool ListId_OverrideForStore { get; set; }
        public IList<SelectListItem> AvailableLists { get; set; }

        [NopResourceDisplayName("Plugins.Misc.MailChimp.Fields.AutoSynchronization")]
        public bool AutoSynchronization { get; set; }

        [NopResourceDisplayName("Plugins.Misc.MailChimp.Fields.SynchronizationPeriod")]
        public int SynchronizationPeriod { get; set; }

        #endregion
    }
}