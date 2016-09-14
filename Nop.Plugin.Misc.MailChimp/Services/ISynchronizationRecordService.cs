using System.Collections.Generic;
using Nop.Core;
using Nop.Plugin.Misc.MailChimp.Domain;

namespace Nop.Plugin.Misc.MailChimp.Services
{
    /// <summary>
    /// MailChimp synchronization record service interface
    /// </summary>
    public partial interface ISynchronizationRecordService
    {
        /// <summary>
        /// Gets all synchronization records
        /// </summary>
        /// <param name="pageIndex">Page index</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>Synchronization records</returns>
        IPagedList<MailChimpSynchronizationRecord> GetAllRecords(int pageIndex = 0, int pageSize = int.MaxValue);

        /// <summary>
        /// Gets a synchronization record
        /// </summary>
        /// <param name="recordId">Synchronization record identifier</param>
        /// <returns>Synchronization record</returns>
        MailChimpSynchronizationRecord GetRecordById(int recordId);

        /// <summary>
        /// Gets a synchronization record by entity type and entity id
        /// </summary>
        /// <param name="entityType">Entity type</param>
        /// <param name="entityId">Entity identidier</param>
        /// <returns>Synchronization record</returns>
        MailChimpSynchronizationRecord GetRecordByEntityTypeAndEntityId(EntityType entityType, int entityId);

        /// <summary>
        /// Gets synchronization records by entity type and action type
        /// </summary>
        /// <param name="entityType">Entity type</param>
        /// <param name="actionType">Action type</param>
        /// <returns>Synchronization records</returns>
        IList<MailChimpSynchronizationRecord> GetRecordsByEntityTypeAndActionType(EntityType entityType, ActionType actionType);

        /// <summary>
        /// Create or update existing syncgronization record
        /// </summary>
        /// <param name="entityType">Entity type</param>
        /// <param name="entityId">Entity identidier</param>
        /// <param name="actionType">Action type</param>
        /// <param name="email">Email (only for subscriptions)</param>
        /// <param name="productId">Product identifier (for product attributes, attribute values and attribute combinations)</param>
        void CreateOrUpdateRecord(EntityType entityType, int entityId, ActionType actionType, string email = null, int productId = 0);

        /// <summary>
        /// Inserts a synchronization record
        /// </summary>
        /// <param name="record">Synchronization record</param>
        void InsertRecord(MailChimpSynchronizationRecord record);

        /// <summary>
        /// Updates a synchronization record
        /// </summary>
        /// <param name="record">Synchronization record</param>
        void UpdateRecord(MailChimpSynchronizationRecord record);

        /// <summary>
        /// Deletes a synchronization record
        /// </summary>
        /// <param name="record">Synchronization record</param>
        void DeleteRecord(MailChimpSynchronizationRecord record);

        /// <summary>
        /// Deletes synchronization records by entity type
        /// </summary>
        /// <param name="entityType">Entity type</param>
        void DeleteRecordsByEntityType(EntityType entityType);
    }
}
