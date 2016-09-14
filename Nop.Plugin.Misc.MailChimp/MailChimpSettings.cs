using Nop.Core.Configuration;

namespace Nop.Plugin.Misc.MailChimp
{
    public class MailChimpSettings : ISettings
    {
        /// <summary>
        /// Gets or sets the API key
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Gets or sets value indicating whether to use MailChimp E-Commerce API
        /// </summary>
        public bool UseEcommerceApi { get; set; }

        /// <summary>
        /// Gets or sets list identifier of contacts
        /// </summary>
        public string ListId { get; set; }

        /// <summary>
        /// Gets or sets webhook identifier 
        /// </summary>
        public string WebhookId { get; set; }
    }
}