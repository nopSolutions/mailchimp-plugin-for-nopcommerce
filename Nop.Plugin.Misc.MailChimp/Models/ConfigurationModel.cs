using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Nop.Plugin.Misc.MailChimp.Models;

/// <summary>
/// Represents MailChimp configuration model
/// </summary>
public record ConfigurationModel
{
    #region Ctor

    public ConfigurationModel()
    {
        AvailableLists = new List<SelectListItem>();
        NewsLetterSubscriptionTypes = new List<NewsLetterSubscriptionMapModel>();
    }

    #endregion

    #region Properties

    public int ActiveStoreScopeConfiguration { get; set; }

    public bool SynchronizationStarted { get; set; }

    [NopResourceDisplayName("Plugins.Misc.MailChimp.Fields.ApiKey")]
    [DataType(DataType.Password)]
    public string ApiKey { get; set; }

    [NopResourceDisplayName("Plugins.Misc.MailChimp.Fields.AccountInfo")]
    public string AccountInfo { get; set; }

    [NopResourceDisplayName("Plugins.Misc.MailChimp.Fields.PassEcommerceData")]
    public bool PassEcommerceData { get; set; }

    [NopResourceDisplayName("Plugins.Misc.MailChimp.Fields.PassOnlySubscribed")]
    public bool PassOnlySubscribed { get; set; }

    public IList<SelectListItem> AvailableLists { get; set; }

    public IList<NewsLetterSubscriptionMapModel> NewsLetterSubscriptionTypes { get; set; }

    [NopResourceDisplayName("Plugins.Misc.MailChimp.Fields.AutoSynchronization")]
    public bool AutoSynchronization { get; set; }

    [NopResourceDisplayName("Plugins.Misc.MailChimp.Fields.SynchronizationPeriod")]
    public int SynchronizationPeriod { get; set; }

    #endregion
}