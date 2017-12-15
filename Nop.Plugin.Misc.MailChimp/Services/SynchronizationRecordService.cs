using System;
using System.Collections.Generic;
using System.Linq;
using Nop.Core.Data;
using Nop.Plugin.Misc.MailChimp.Domain;

namespace Nop.Plugin.Misc.MailChimp.Services
{
    /// <summary>
    /// Represents MailChimp synchronization record service implementation
    /// </summary>
    public class SynchronizationRecordService : ISynchronizationRecordService
    {
        #region Fields

        private readonly IRepository<MailChimpSynchronizationRecord> _synchronizationRecordRepository;

        #endregion

        #region Ctor

        public SynchronizationRecordService(IRepository<MailChimpSynchronizationRecord> synchronizationRecordRepository)
        {
            this._synchronizationRecordRepository = synchronizationRecordRepository;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Get all synchronization records
        /// </summary>
        /// <returns>List of synchronization records</returns>
        public virtual IList<MailChimpSynchronizationRecord> GetAllRecords()
        {
            return _synchronizationRecordRepository.Table.OrderBy(record => record.Id).ToList();
        }

        /// <summary>
        /// Get a synchronization record by identifier
        /// </summary>
        /// <param name="recordId">Synchronization record identifier</param>
        /// <returns>Synchronization record</returns>
        public virtual MailChimpSynchronizationRecord GetRecordById(int recordId)
        {
            if (recordId == 0)
                return null;

            return _synchronizationRecordRepository.GetById(recordId);
        }

        /// <summary>
        /// Get synchronization records by entity type and operation type
        /// </summary>
        /// <param name="entityType">Entity type</param>
        /// <param name="operationType">Operation type</param>
        /// <returns>List of aynchronization records</returns>
        public virtual IList<MailChimpSynchronizationRecord> GetRecordsByEntityTypeAndOperationType(EntityType entityType, OperationType operationType)
        {
            return _synchronizationRecordRepository.Table.Where(record =>
                record.EntityTypeId == (int)entityType && record.OperationTypeId == (int)operationType).ToList();
        }

        /// <summary>
        /// Create the new one or update an existing synchronization record
        /// </summary>
        /// <param name="entityType">Entity type</param>
        /// <param name="entityId">Entity identifier</param>
        /// <param name="operationType">Operation type</param>
        /// <param name="email">Email (only for subscriptions)</param>
        /// <param name="productId">Product identifier (for product attributes, attribute values and attribute combinations)</param>
        public virtual void CreateOrUpdateRecord(EntityType entityType, int entityId, OperationType operationType, string email = null, int productId = 0)
        {
            //whether the synchronization record with passed parameters already exists
            var existingRecord = _synchronizationRecordRepository.Table
                .FirstOrDefault(record => record.EntityTypeId == (int)entityType && record.EntityId == entityId);
            if (existingRecord == null)
            {
                //create the new one if not exists
                InsertRecord(new MailChimpSynchronizationRecord
                {
                    EntityType = entityType,
                    EntityId = entityId,
                    OperationType = operationType,
                    Email = email,
                    ProductId = productId
                });
                return;
            }

            //or update the existing
            switch (existingRecord.OperationType)
            {
                case OperationType.Create:
                    if (operationType == OperationType.Delete)
                        DeleteRecord(existingRecord);
                    return;

                case OperationType.Update:
                    if (operationType == OperationType.Delete)
                    {
                        existingRecord.OperationType = OperationType.Delete;
                        UpdateRecord(existingRecord);
                    }
                    return;

                case OperationType.Delete:
                    if (operationType == OperationType.Create)
                    {
                        existingRecord.OperationType = OperationType.Update;
                        UpdateRecord(existingRecord);
                    }
                    return;
            }
        }

        /// <summary>
        /// Insert a synchronization record
        /// </summary>
        /// <param name="record">Synchronization record</param>
        public virtual void InsertRecord(MailChimpSynchronizationRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            _synchronizationRecordRepository.Insert(record);
        }

        /// <summary>
        /// Update the synchronization record
        /// </summary>
        /// <param name="record">Synchronization record</param>
        public virtual void UpdateRecord(MailChimpSynchronizationRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            _synchronizationRecordRepository.Update(record);
        }

        /// <summary>
        /// Delete a synchronization record
        /// </summary>
        /// <param name="record">Synchronization record</param>
        public virtual void DeleteRecord(MailChimpSynchronizationRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            _synchronizationRecordRepository.Delete(record);
        }

        /// <summary>
        /// Delete synchronization records by entity type
        /// </summary>
        /// <param name="entityType">Entity type</param>
        public virtual void DeleteRecordsByEntityType(EntityType entityType)
        {
            var records = GetAllRecords().Where(record => record.EntityType == entityType);
            _synchronizationRecordRepository.Delete(records);
        }

        /// <summary>
        /// Delete all synchronization records
        /// </summary>
        public virtual void ClearRecords()
        {
            _synchronizationRecordRepository.Delete(GetAllRecords());
        }

        #endregion
    }
}