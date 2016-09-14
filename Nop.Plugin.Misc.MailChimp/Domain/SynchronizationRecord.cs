using Nop.Core;

namespace Nop.Plugin.Misc.MailChimp.Domain
{
    /// <summary>
    /// Represents a record pointing at the entity ready to synchronization
    /// </summary>
    public partial class MailChimpSynchronizationRecord : BaseEntity
    {
        /// <summary>
        /// Gets or sets an entity type identifier
        /// </summary>
        public int EntityTypeId { get; set; }

        /// <summary>
        /// Gets or sets an entity identidier
        /// </summary>
        public int EntityId { get; set; }

        /// <summary>
        /// Gets or sets an email (only for subscriptions)
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Gets or sets a product identidier (for product attributes, attribute values and attribute combinations)
        /// </summary>
        public int ProductId { get; set; }

        /// <summary>
        /// Gets or sets an action type identifier
        /// </summary>
        public int ActionTypeId { get; set; }

        /// <summary>
        /// Gets or sets an entity type 
        /// </summary>
        public EntityType EntityType
        {
            get { return (EntityType)EntityTypeId; }
            set { EntityTypeId = (int)value; }
        }

        /// <summary>
        /// Gets or sets an action type 
        /// </summary>
        public ActionType ActionType
        {
            get { return (ActionType)ActionTypeId; }
            set { ActionTypeId = (int)value; }
        }
    }

    /// <summary>
    /// Represents an entity type
    /// </summary>
    public enum EntityType
    {
        Store,
        Customer,
        Subscription,
        Order,
        Product,
        ProductAttribute,
        AttributeValue,
        AttributeCombination
    }

    /// <summary>
    /// Represents an action type
    /// </summary>
    public enum ActionType
    {
        Read = 1,
        Create = 2,
        Update = 4,
        Delete = 8
    }
}