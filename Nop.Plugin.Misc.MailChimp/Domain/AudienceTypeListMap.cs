namespace Nop.Plugin.Misc.MailChimp.Domain
{
    /// <summary>
    /// Represents a mapping between a subsscription type list and an audience in MailChimp
    /// </summary>
    public class AudienceTypeListMap
    {
        /// <summary>
        /// Gets or sets the type-list identifier
        /// </summary>
        public int TypeListId { get; set; }

        /// <summary>
        /// Gets or sets the audience identifier
        /// </summary>
        public string AudienceId { get; set; }
    }
}
