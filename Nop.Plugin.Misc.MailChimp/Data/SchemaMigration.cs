using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Nop.Plugin.Misc.MailChimp.Domain;

namespace Nop.Plugin.Misc.MailChimp.Data
{
    [NopMigration("2020/06/04 12:00:00", "Misc.MailChimp base schema", MigrationProcessType.Installation)]
    public class SchemaMigration : AutoReversingMigration
    {
        #region Methods

        /// <summary>
        /// Collect the UP migration expressions
        /// </summary>
        public override void Up()
        {
            Create.TableFor<MailChimpSynchronizationRecord>();
        }

        #endregion
    }
}
