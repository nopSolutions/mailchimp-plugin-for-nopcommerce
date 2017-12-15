using Nop.Data.Mapping;
using Nop.Plugin.Misc.MailChimp.Domain;

namespace Nop.Plugin.Misc.MailChimp.Data
{
    /// <summary>
    /// Represents a mapping class for synchronization record
    /// </summary>
    public class SynchronizationRecordMap : NopEntityTypeConfiguration<MailChimpSynchronizationRecord>
    {
        public SynchronizationRecordMap()
        {
            this.ToTable(nameof(MailChimpSynchronizationRecord));
            this.HasKey(record => record.Id);
            this.Ignore(record => record.EntityType);
            this.Ignore(record => record.OperationType);
            this.Property(record => record.Email).HasMaxLength(255);
        }
    }
}