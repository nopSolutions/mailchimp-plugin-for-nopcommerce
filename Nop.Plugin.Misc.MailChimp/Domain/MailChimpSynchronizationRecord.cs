using Nop.Core;

namespace Nop.Plugin.Misc.MailChimp.Domain
{
    /// <summary>
    /// Represents a record pointing at the entity ready to synchronization
    /// </summary>
    public class MailChimpSynchronizationRecord : BaseEntity
    {
        /// <summary>
        /// Gets or sets an entity type identifier
        /// </summary>
        public int EntityTypeId { get; set; }

        /// <summary>
        /// Gets or sets an entity identifier
        /// </summary>
        public int EntityId { get; set; }

        /// <summary>
        /// Gets or sets an operation type identifier
        /// </summary>
        public int OperationTypeId { get; set; }

        /// <summary>
        /// Gets or sets an email (used only for subscriptions)
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Gets or sets a product identifier (used only for product attribute combinations)
        /// </summary>
        public int ProductId { get; set; }

        /// <summary>
        /// Gets or sets an entity type 
        /// </summary>
        public EntityType EntityType
        {
            get { return (EntityType)EntityTypeId; }
            set { EntityTypeId = (int)value; }
        }

        /// <summary>
        /// Gets or sets an operation type 
        /// </summary>
        public OperationType OperationType
        {
            get { return (OperationType)OperationTypeId; }
            set { OperationTypeId = (int)value; }
        }
    }
}