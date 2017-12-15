using System.Collections.Generic;
using Nop.Plugin.Misc.MailChimp.Domain;

namespace Nop.Plugin.Misc.MailChimp.Services
{
    /// <summary>
    /// Represents MailChimp synchronization record service
    /// </summary>
    public interface ISynchronizationRecordService
    {
        /// <summary>
        /// Get all synchronization records
        /// </summary>
        /// <returns>List of synchronization records</returns>
        IList<MailChimpSynchronizationRecord> GetAllRecords();

        /// <summary>
        /// Get a synchronization record by identifier
        /// </summary>
        /// <param name="recordId">Synchronization record identifier</param>
        /// <returns>Synchronization record</returns>
        MailChimpSynchronizationRecord GetRecordById(int recordId);

        /// <summary>
        /// Get synchronization records by entity type and operation type
        /// </summary>
        /// <param name="entityType">Entity type</param>
        /// <param name="operationType">Operation type</param>
        /// <returns>List of aynchronization records</returns>
        IList<MailChimpSynchronizationRecord> GetRecordsByEntityTypeAndOperationType(EntityType entityType, OperationType operationType);

        /// <summary>
        /// Create the new one or update an existing synchronization record
        /// </summary>
        /// <param name="entityType">Entity type</param>
        /// <param name="entityId">Entity identifier</param>
        /// <param name="operationType">Operation type</param>
        /// <param name="email">Email (only for subscriptions)</param>
        /// <param name="productId">Product identifier (for product attributes, attribute values and attribute combinations)</param>
        void CreateOrUpdateRecord(EntityType entityType, int entityId, OperationType operationType, string email = null, int productId = 0);

        /// <summary>
        /// Insert a synchronization record
        /// </summary>
        /// <param name="record">Synchronization record</param>
        void InsertRecord(MailChimpSynchronizationRecord record);

        /// <summary>
        /// Update a synchronization record
        /// </summary>
        /// <param name="record">Synchronization record</param>
        void UpdateRecord(MailChimpSynchronizationRecord record);

        /// <summary>
        /// Delete a synchronization record
        /// </summary>
        /// <param name="record">Synchronization record</param>
        void DeleteRecord(MailChimpSynchronizationRecord record);

        /// <summary>
        /// Delete synchronization records by entity type
        /// </summary>
        /// <param name="entityType">Entity type</param>
        void DeleteRecordsByEntityType(EntityType entityType);

        /// <summary>
        /// Delete all synchronization records
        /// </summary>
        void ClearRecords();
    }
}