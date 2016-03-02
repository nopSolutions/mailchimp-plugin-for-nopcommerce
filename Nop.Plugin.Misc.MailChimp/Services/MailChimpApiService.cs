using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Nop.Plugin.Misc.MailChimp.Data;
using Nop.Services.Logging;
using MailChimp;
using MailChimp.Lists;
using MailChimp.Helper;

namespace Nop.Plugin.Misc.MailChimp.Services
{
    public class MailChimpApiService : IMailChimpApiService
    {
        private readonly MailChimpSettings _mailChimpSettings;
        private readonly ISubscriptionEventQueueingService _subscriptionEventQueueingService;
        private readonly ILogger _log;

        public MailChimpApiService(MailChimpSettings mailChimpSettings, ISubscriptionEventQueueingService subscriptionEventQueueingService, ILogger log)
        {
            _mailChimpSettings = mailChimpSettings;
            _subscriptionEventQueueingService = subscriptionEventQueueingService;
            _log = log;
        }

        /// <summary>
        /// Retrieves the lists.
        /// </summary>
        /// <returns></returns>
        public virtual NameValueCollection RetrieveLists()
        {
            var output = new NameValueCollection();
            try
            {
                // input parameters
                MailChimpManager mc = new MailChimpManager(_mailChimpSettings.ApiKey);

                ListResult lists = mc.GetLists();
                foreach (var list in lists.Data)
                {
                    output.Add(list.Name, list.Id);
                }
            }
            catch (Exception e)
            {
                _log.Debug(e.Message, e);
            }
            return output;
        }

        /// <summary>
        /// Batches the unsubscribe.
        /// </summary>
        /// <param name="recordList">The records</param>
        public virtual BatchUnsubscribeResult BatchUnsubscribe(IEnumerable<MailChimpEventQueueRecord> recordList)
        {
            if (String.IsNullOrEmpty(_mailChimpSettings.DefaultListId)) 
                throw new ArgumentException("MailChimp list is not specified");

            MailChimpManager mc = new MailChimpManager(_mailChimpSettings.ApiKey);
            List<EmailParameter> batch = new List<EmailParameter>();

            foreach (var sub in recordList)
            {
                EmailParameter email = new EmailParameter()
                {
                    Email = sub.Email
                };
                batch.Add(email);
            }

            BatchUnsubscribeResult results = mc.BatchUnsubscribe(
                listId: _mailChimpSettings.DefaultListId,
                listOfEmails: batch
                //sendGoodbye: false
            );

            return results;
        }

        /// <summary>
        /// Batches the subscribe.
        /// </summary>
        /// <param name="recordList">The records</param>
        public virtual BatchSubscribeResult BatchSubscribe(IEnumerable<MailChimpEventQueueRecord> recordList)
        {
            if (String.IsNullOrEmpty(_mailChimpSettings.DefaultListId)) 
                throw new ArgumentException("MailChimp list is not specified");
            
            MailChimpManager mc = new MailChimpManager(_mailChimpSettings.ApiKey);
            List<BatchEmailParameter> batch = new List<BatchEmailParameter>();

            foreach (var sub in recordList)
            {
                BatchEmailParameter email = new BatchEmailParameter()
                {
                    Email = new EmailParameter()
                    {
                        Email = sub.Email
                    }
                };
                batch.Add(email);
            }

            BatchSubscribeResult results = mc.BatchSubscribe(
                listId: _mailChimpSettings.DefaultListId,
                listOfEmails: batch,
                doubleOptIn: false,
                updateExisting: true,
                replaceInterests: true
            );

            return results;
        }
        
        public virtual SyncResult Synchronize()
        {
            var result = new SyncResult();

            // Get all the queued records for subscription/unsubscription
            var allRecords = _subscriptionEventQueueingService.GetAll();
            //get unique and latest records
            var allRecordsUnique = new List<MailChimpEventQueueRecord>();
            foreach (var item in allRecords
                .OrderByDescending(x => x.CreatedOnUtc))
            {
                var exists = allRecordsUnique
                    .Where(x => x.Email.Equals(item.Email, StringComparison.InvariantCultureIgnoreCase))
                    .FirstOrDefault() != null;
                if (!exists)
                    allRecordsUnique.Add(item);
            }
            var subscribeRecords = allRecordsUnique.Where(x => x.IsSubscribe).ToList();
            var unsubscribeRecords = allRecordsUnique.Where(x => !x.IsSubscribe).ToList();
            
            //subscribe
            if (subscribeRecords.Count > 0)
            {
                var subscribeResult = BatchSubscribe(subscribeRecords);
                //result
                result.SubscribeResult = subscribeResult.AddCount.ToString() + " record(s) have been subscribed ("
                    + subscribeResult.UpdateCount.ToString() + " updated). ";

                if (subscribeResult.ErrorCount > 0)
                {
                    result.SubscribeResult += subscribeResult.ErrorCount.ToString() + " error(s).";

                    foreach (var error in subscribeResult.Errors)
                        result.SubscribeErrors.Add(error.ErrorMessage);
                }
            }
            else
            {
                result.SubscribeResult = "No records to add";
            }
            //unsubscribe
            if (unsubscribeRecords.Count > 0)
            {
                var unsubscribeResult = BatchUnsubscribe(unsubscribeRecords);
                //result
                result.UnsubscribeResult = unsubscribeResult.SuccessCount + " record(s) have been unsubscribed. ";

                if (unsubscribeResult.ErrorCount > 0)
                {
                    result.UnsubscribeResult += unsubscribeResult.ErrorCount.ToString() + " error(s).";

                    foreach (var error in unsubscribeResult.Errors)
                        result.UnsubscribeErrors.Add(error.ErrorMessage);
                }
            }
            else
            {
                result.UnsubscribeResult = "No records to unsubscribe";
            }

            //delete the queued records
            foreach (var sub in allRecords)
                _subscriptionEventQueueingService.Delete(sub);

            return result;
        }
    }
}