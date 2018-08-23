using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nop.Data.Mapping;
using Nop.Plugin.Misc.MailChimp.Domain;

namespace Nop.Plugin.Misc.MailChimp.Data
{
    /// <summary>
    /// Represents a mapping class for synchronization record
    /// </summary>
    public partial class SynchronizationRecordMap : NopEntityTypeConfiguration<MailChimpSynchronizationRecord>
    {
        #region Methods

        /// <summary>
        /// Configures the entity
        /// </summary>
        /// <param name="builder">The builder to be used to configure the entity</param>
        public override void Configure(EntityTypeBuilder<MailChimpSynchronizationRecord> builder)
        {
            builder.ToTable(nameof(MailChimpSynchronizationRecord));
            builder.HasKey(record => record.Id);
            builder.Ignore(record => record.EntityType);
            builder.Ignore(record => record.OperationType);
            builder.Property(record => record.Email).HasMaxLength(255);
        }

        #endregion        
    }
}