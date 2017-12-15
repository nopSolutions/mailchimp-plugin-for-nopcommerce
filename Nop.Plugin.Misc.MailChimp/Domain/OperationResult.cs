using MailChimp.Net.Models;
using Newtonsoft.Json;

namespace Nop.Plugin.Misc.MailChimp.Domain
{
    /// <summary>
    /// Represents operation result
    /// </summary>
    public class OperationResult
    {
        [JsonProperty(PropertyName = "status_code")]
        public string StatusCode { get; set; }

        [JsonProperty(PropertyName = "operation_id")]
        public string OperationId { get; set; }

        [JsonProperty(PropertyName = "response")]
        public string ResponseString { get; set; }
    }
}