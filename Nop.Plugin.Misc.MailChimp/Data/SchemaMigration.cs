using FluentMigrator;
using Nop.Data.Migrations;
using Nop.Plugin.Misc.MailChimp.Domain;

namespace Nop.Plugin.Misc.MailChimp.Data
{
    [SkipMigrationOnUpdate]
    [NopMigration("2020/06/04 12:00:00", "Misc.MailChimpl base schema")]
    public class SchemaMigration : AutoReversingMigration
    {
        #region Fields

        protected IMigrationManager _migrationManager;

        #endregion

        #region Ctor

        public SchemaMigration(IMigrationManager migrationManager)
        {
            _migrationManager = migrationManager;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Collect the UP migration expressions
        /// </summary>
        public override void Up()
        {
            _migrationManager.BuildTable<MailChimpSynchronizationRecord>(Create);
        }

        #endregion
    }
}
