using Nop.Core.Configuration;

namespace Nop.Plugin.Misc.MailChimp
{
    /// <summary>
    /// Represents MailChimp plugin settings
    /// </summary>
    public class MailChimpSettings : ISettings
    {
        /// <summary>
        /// Gets or sets the API key
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Gets or sets value indicating whether to pass E-Commerce data (customers, products, orders, etc) to MailChimp
        /// </summary>
        public bool PassEcommerceData { get; set; }

        /// <summary>
        /// Gets or sets identifier of user list
        /// </summary>
        public string ListId { get; set; }

        /// <summary>
        /// Gets or sets mask of store identifier that uniquely identifying the store in MailChimp E-Commerce
        /// </summary>
        public string StoreIdMask { get; set; }

        /// <summary>
        /// Gets or sets number of an operation in the batch 
        /// </summary>
        public int BatchOperationNumber { get; set; }
    }
}