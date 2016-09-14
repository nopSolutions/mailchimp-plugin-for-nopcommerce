using Nop.Data.Mapping;
using Nop.Plugin.Misc.MailChimp.Domain;

namespace Nop.Plugin.Misc.MailChimp.Data
{
    public partial class SynchronizationRecordMap : NopEntityTypeConfiguration<MailChimpSynchronizationRecord>
    {
        public SynchronizationRecordMap()
        {
            this.ToTable("MailChimpSynchronizationRecord");
            this.HasKey(record => record.Id);
            this.Ignore(record => record.EntityType);
            this.Ignore(record => record.ActionType);
            this.Property(record => record.Email).HasMaxLength(255);
        }
    }
}