using System;
using System.Collections.Generic;
using System.Linq;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Data;
using Nop.Plugin.Misc.MailChimp.Domain;

namespace Nop.Plugin.Misc.MailChimp.Services
{
    /// <summary>
    /// MailChimp synchronization record service
    /// </summary>
    public partial class SynchronizationRecordService : ISynchronizationRecordService
    {
        #region Constants

        private const string SYNCHRONIZATION_RECORD_ALL_KEY = "Nop.synchronizationRecord.all-{0}-{1}";
        private const string SYNCHRONIZATION_RECORD_PATTERN_KEY = "Nop.synchronizationRecord.";
       
        #endregion

        #region Fields

        private readonly ICacheManager _cacheManager;
        private readonly IRepository<MailChimpSynchronizationRecord> _synchronizationRecordRepository;

        #endregion

        #region Ctor

        public SynchronizationRecordService(ICacheManager cacheManager,
            IRepository<MailChimpSynchronizationRecord> synchronizationRecordRepository)
        {
            this._cacheManager = cacheManager;
            this._synchronizationRecordRepository = synchronizationRecordRepository;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets all synchronization records
        /// </summary>
        /// <param name="pageIndex">Page index</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>Synchronization records</returns>
        public virtual IPagedList<MailChimpSynchronizationRecord> GetAllRecords(int pageIndex = 0, int pageSize = int.MaxValue)
        {
            var key = string.Format(SYNCHRONIZATION_RECORD_ALL_KEY, pageIndex, pageSize);
            var query = _synchronizationRecordRepository.Table.OrderBy(record => record.Id);
            return _cacheManager.Get(key, () => new PagedList<MailChimpSynchronizationRecord>(query, pageIndex, pageSize));
        }

        /// <summary>
        /// Gets a synchronization record
        /// </summary>
        /// <param name="recordId">Synchronization record identifier</param>
        /// <returns>Synchronization record</returns>
        public virtual MailChimpSynchronizationRecord GetRecordById(int recordId)
        {
           return recordId > 0 ? _synchronizationRecordRepository.GetById(recordId) : null;
        }

        /// <summary>
        /// Gets a synchronization record by entity type and entity id
        /// </summary>
        /// <param name="entityType">Entity type</param>
        /// <param name="entityId">Entity id</param>
        /// <returns>Synchronization record</returns>
        public virtual MailChimpSynchronizationRecord GetRecordByEntityTypeAndEntityId(EntityType entityType, int entityId)
        {
            return _synchronizationRecordRepository.Table.FirstOrDefault(record => record.EntityTypeId == (int)entityType && record.EntityId == entityId);
        }

        /// <summary>
        /// Gets synchronization records by entity type and action type
        /// </summary>
        /// <param name="entityType">Entity type</param>
        /// <param name="actionType">Action type</param>
        /// <returns>Synchronization records</returns>
        public virtual IList<MailChimpSynchronizationRecord> GetRecordsByEntityTypeAndActionType(EntityType entityType, ActionType actionType)
        {
            return _synchronizationRecordRepository.Table.Where(record => 
                record.EntityTypeId == (int)entityType && record.ActionTypeId == (int)actionType).ToList();
        }

        /// <summary>
        /// Create or update existing syncgronization record
        /// </summary>
        /// <param name="entityType">Entity type</param>
        /// <param name="entityId">Entity identidier</param>
        /// <param name="actionType">Action type</param>
        /// <param name="email">Email (only for subscriptions)</param>
        /// <param name="productId">Product identifier (for product attributes, attribute values and attribute combinations)</param>
        public virtual void CreateOrUpdateRecord(EntityType entityType, int entityId, ActionType actionType, string email = null, int productId = 0)
        {
            var existingRecord = GetRecordByEntityTypeAndEntityId(entityType, entityId);
            if (existingRecord != null)
            {
                switch (existingRecord.ActionType)
                {
                    case ActionType.Create:
                        if (actionType == ActionType.Delete)
                            DeleteRecord(existingRecord);
                        break;
                    case ActionType.Update:
                        if (actionType == ActionType.Delete)
                        {
                            existingRecord.ActionType = ActionType.Delete;
                            UpdateRecord(existingRecord);
                        }
                        break;
                    case ActionType.Delete:
                        if (actionType == ActionType.Create)
                        {
                            existingRecord.ActionType = ActionType.Update;
                            UpdateRecord(existingRecord);
                        }
                        break;
                }
            }
            else
                InsertRecord(new MailChimpSynchronizationRecord
                {
                    EntityType = entityType,
                    EntityId = entityId,
                    ActionType = actionType,
                    Email = email,
                    ProductId = productId
                });
        }

        /// <summary>
        /// Inserts a synchronization record
        /// </summary>
        /// <param name="record">Synchronization record</param>
        public virtual void InsertRecord(MailChimpSynchronizationRecord record)
        {
            if (record == null)
                throw new ArgumentNullException("record");

            _synchronizationRecordRepository.Insert(record);
            _cacheManager.RemoveByPattern(SYNCHRONIZATION_RECORD_PATTERN_KEY);
        }

        /// <summary>
        /// Updates the synchronization record
        /// </summary>
        /// <param name="record">Synchronization record</param>
        public virtual void UpdateRecord(MailChimpSynchronizationRecord record)
        {
            if (record == null)
                throw new ArgumentNullException("record");

            _synchronizationRecordRepository.Update(record);
            _cacheManager.RemoveByPattern(SYNCHRONIZATION_RECORD_PATTERN_KEY);
        }

        /// <summary>
        /// Deletes a synchronization record
        /// </summary>
        /// <param name="record">Synchronization record</param>
        public virtual void DeleteRecord(MailChimpSynchronizationRecord record)
        {
            if (record == null)
                throw new ArgumentNullException("record");

            _synchronizationRecordRepository.Delete(record);
            _cacheManager.RemoveByPattern(SYNCHRONIZATION_RECORD_PATTERN_KEY);
        }

        /// <summary>
        /// Deletes synchronization records by entity type
        /// </summary>
        /// <param name="entityType">Entity type</param>
        public virtual void DeleteRecordsByEntityType(EntityType entityType)
        {
            var records = GetAllRecords().Where(record => record.EntityType == entityType).ToList();
            foreach (var record in records)
            {
                DeleteRecord(record);
            }
        }

        #endregion
    }
}
