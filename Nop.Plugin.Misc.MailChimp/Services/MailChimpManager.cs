using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MailChimp.Net.Core;
using MailChimp.Net.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Core.Html;
using Nop.Plugin.Misc.MailChimp.Domain;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Seo;
using Nop.Services.Stores;
using SharpCompress.Readers;
using mailchimp = MailChimp.Net.Models;

namespace Nop.Plugin.Misc.MailChimp.Services
{
    /// <summary>
    /// Represents MailChimp manager
    /// </summary>
    public class MailChimpManager
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly ICountryService _countryService;
        private readonly ICurrencyService _currencyService;
        private readonly ICustomerService _customerService;
        private readonly IDateTimeHelper _dateTimeHelper;
        private readonly ILanguageService _languageService;
        private readonly ILogger _logger;
        private readonly INewsLetterSubscriptionService _newsLetterSubscriptionService;
        private readonly IOrderService _orderService;
        private readonly IPictureService _pictureService;
        private readonly IPriceCalculationService _priceCalculationService;
        private readonly IProductAttributeParser _productAttributeParser;
        private readonly IProductAttributeService _productAttributeService;
        private readonly IProductService _productService;
        private readonly ISettingService _settingService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly IStoreMappingService _storeMappingService;
        private readonly IStoreService _storeService;
        private readonly ISynchronizationRecordService _synchronizationRecordService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly MailChimpSettings _mailChimpSettings;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IMailChimpManager _mailChimpManager;
        private readonly IUrlRecordService _urlRecordService;

        #endregion

        #region Ctor

        public MailChimpManager(CurrencySettings currencySettings,
            IActionContextAccessor actionContextAccessor,
            ICountryService countryService,
            ICurrencyService currencyService,
            ICustomerService customerService,
            IDateTimeHelper dateTimeHelper,
            ILanguageService languageService,
            ILogger logger,
            INewsLetterSubscriptionService newsLetterSubscriptionService,
            IOrderService orderService,
            IPictureService pictureService,
            IPriceCalculationService priceCalculationService,
            IProductAttributeParser productAttributeParser,
            IProductAttributeService productAttributeService,
            IProductService productService,
            ISettingService settingService,
            IStateProvinceService stateProvinceService,
            IStoreMappingService storeMappingService,
            IStoreService storeService,
            ISynchronizationRecordService synchronizationRecordService,
            IUrlHelperFactory urlHelperFactory,
            IWebHelper webHelper,
            IWorkContext workContext,
            IGenericAttributeService genericAttributeService,
            MailChimpSettings mailChimpSettings,
            IUrlRecordService urlRecordService)
        {
            this._currencySettings = currencySettings;
            this._actionContextAccessor = actionContextAccessor;
            this._countryService = countryService;
            this._currencyService = currencyService;
            this._customerService = customerService;
            this._dateTimeHelper = dateTimeHelper;
            this._languageService = languageService;
            this._logger = logger;
            this._newsLetterSubscriptionService = newsLetterSubscriptionService;
            this._orderService = orderService;
            this._pictureService = pictureService;
            this._priceCalculationService = priceCalculationService;
            this._productAttributeParser = productAttributeParser;
            this._productAttributeService = productAttributeService;
            this._productService = productService;
            this._settingService = settingService;
            this._stateProvinceService = stateProvinceService;
            this._storeMappingService = storeMappingService;
            this._storeService = storeService;
            this._synchronizationRecordService = synchronizationRecordService;
            this._urlHelperFactory = urlHelperFactory;
            this._webHelper = webHelper;
            this._workContext = workContext;
            this._genericAttributeService = genericAttributeService;
            this._mailChimpSettings = mailChimpSettings;
            this._urlRecordService = urlRecordService;

            //create wrapper MailChimp manager
            if (!string.IsNullOrEmpty(_mailChimpSettings.ApiKey))
                _mailChimpManager = new global::MailChimp.Net.MailChimpManager(_mailChimpSettings.ApiKey);
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Handle request
        /// </summary>
        /// <typeparam name="T">Output type</typeparam>
        /// <param name="request">Request actions</param>
        /// <returns>The asynchronous task whose result contains the object of T type</returns>
        private async Task<T> HandleRequest<T>(Func<Task<T>> request)
        {
            try
            {
                //ensure that plugin is configured
                if (_mailChimpManager == null)
                    throw new NopException("Plugin is not configured");

                return await request();
            }
            catch (Exception exception)
            {
                //compose an error message
                var errorMessage = exception.Message;
                if (exception is MailChimpException mailChimpException)
                {
                    errorMessage = $"{mailChimpException.Status} {mailChimpException.Title} - {mailChimpException.Detail}{Environment.NewLine}";
                    if (mailChimpException.Errors?.Any() ?? false)
                    {
                        var errorDetails = mailChimpException.Errors
                            .Aggregate(string.Empty, (error, detail) => $"{error}{detail?.Field} - {detail?.Message}{Environment.NewLine}");
                        errorMessage = $"{errorMessage} Errors: {errorDetails}";
                    }
                }

                //log errors
                _logger.Error($"MailChimp error. {errorMessage}", exception, _workContext.CurrentCustomer);

                return default(T);
            }
        }

        #region Synchronization

        /// <summary>
        /// Prepare records for the manual synchronization
        /// </summary>
        /// <returns>The asynchronous task whose result determines whether the records prepared</returns>
        private async Task<bool> PrepareRecordsToManualSynchronization()
        {
            return await HandleRequest(async () =>
            {
                //whether to clear existing E-Commerce data
                if (_mailChimpSettings.PassEcommerceData)
                {
                    //get store identifiers
                    var allStoresIds = _storeService.GetAllStores().Select(store => string.Format(_mailChimpSettings.StoreIdMask, store.Id));

                    //get number of stores
                    var storeNumber = (await _mailChimpManager.ECommerceStores.GetResponseAsync())?.TotalItems
                        ?? throw new NopException("No response from the service");

                    //delete all existing E-Commerce data from MailChimp
                    var existingStoresIds = await _mailChimpManager.ECommerceStores
                        .GetAllAsync(new QueryableBaseRequest { FieldsToInclude = "stores.id", Limit = storeNumber })
                        ?? throw new NopException("No response from the service");
                    foreach (var storeId in existingStoresIds.Select(store => store.Id).Intersect(allStoresIds))
                    {
                        await _mailChimpManager.ECommerceStores.DeleteAsync(storeId);
                    }

                    //clear records
                    _synchronizationRecordService.ClearRecords();

                }
                else
                    _synchronizationRecordService.DeleteRecordsByEntityType(EntityType.Subscription);

                //and create initial data
                CreateInitialData();

                return true;
            });
        }

        /// <summary>
        /// Create data for the manual synchronization
        /// </summary>
        private void CreateInitialData()
        {
            //add all subscriptions
            foreach (var subscription in _newsLetterSubscriptionService.GetAllNewsLetterSubscriptions())
            {
                _synchronizationRecordService.InsertRecord(new MailChimpSynchronizationRecord
                {
                    EntityType = EntityType.Subscription,
                    EntityId = subscription.Id,
                    OperationType = OperationType.Create
                });
            }

            //check whether to pass E-Commerce data
            if (!_mailChimpSettings.PassEcommerceData)
                return;

            //add stores
            foreach (var store in _storeService.GetAllStores())
            {
                _synchronizationRecordService.InsertRecord(new MailChimpSynchronizationRecord
                {
                    EntityType = EntityType.Store,
                    EntityId = store.Id,
                    OperationType = OperationType.Create
                });
            }

            //add registered customers
            foreach (var customer in _customerService.GetAllCustomers().Where(customer => !customer.IsGuest()))
            {
                _synchronizationRecordService.InsertRecord(new MailChimpSynchronizationRecord
                {
                    EntityType = EntityType.Customer,
                    EntityId = customer.Id,
                    OperationType = OperationType.Create
                });
            }

            //add products
            foreach (var product in _productService.SearchProducts())
            {
                _synchronizationRecordService.InsertRecord(new MailChimpSynchronizationRecord
                {
                    EntityType = EntityType.Product,
                    EntityId = product.Id,
                    OperationType = OperationType.Create
                });
            }

            //add orders
            foreach (var order in _orderService.SearchOrders())
            {
                _synchronizationRecordService.InsertRecord(new MailChimpSynchronizationRecord
                {
                    EntityType = EntityType.Order,
                    EntityId = order.Id,
                    OperationType = OperationType.Create
                });
            }
        }

        /// <summary>
        /// Prepare batch webhook before the synchronization
        /// </summary>
        /// <returns>The asynchronous task whose result determines whether the batch webhook prepared</returns>
        private async Task<bool> PrepareBatchWebhook()
        {
            return await HandleRequest(async () =>
            {
                //get all batch webhooks 
                var allBatchWebhooks = await _mailChimpManager.BatchWebHooks.GetAllAsync(new QueryableBaseRequest { Limit = int.MaxValue })
                    ?? throw new NopException("No response from the service");

                //generate webhook URL
                var webhookUrl = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext)
                    .RouteUrl(MailChimpDefaults.BatchWebhookRoute, null, _actionContextAccessor.ActionContext.HttpContext.Request.Scheme);

                //create the new one if not exists
                var batchWebhook = allBatchWebhooks.FirstOrDefault(webhook => !string.IsNullOrEmpty(webhook.Url) && webhook.Url.Equals(webhookUrl, StringComparison.InvariantCultureIgnoreCase));
                if (string.IsNullOrEmpty(batchWebhook?.Id))
                {
                    batchWebhook = await _mailChimpManager.BatchWebHooks.AddAsync(webhookUrl)
                        ?? throw new NopException("No response from the service");
                }

                return !string.IsNullOrEmpty(batchWebhook.Id);
            });
        }

        /// <summary>
        /// Create operation to manage MailChimp data
        /// </summary>
        /// <typeparam name="T">Type of object value</typeparam>
        /// <param name="objectValue">Object value</param>
        /// <param name="operationType">Operation type</param>
        /// <param name="requestPath">Path of API request</param>
        /// <param name="operationId">Operation ID</param>
        /// <param name="additionalData">Additional parameters</param>
        /// <returns>Operation</returns>
        private Operation CreateOperation<T>(T objectValue, OperationType operationType,
            string requestPath, string operationId, object additionalData = null)
        {
            return new Operation
            {
                Method = GetWebMethod(operationType),
                OperationId = operationId,
                Path = requestPath,
                Body = JsonConvert.SerializeObject(objectValue, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                Params = additionalData,
            };
        }

        /// <summary>
        /// Get web request method for the passed operation type
        /// </summary>
        /// <param name="operationType">Operation type</param>
        /// <returns>Method name</returns>
        private string GetWebMethod(OperationType operationType)
        {
            switch (operationType)
            {
                case OperationType.Read:
                    return WebRequestMethods.Http.Get;

                case OperationType.Create:
                    return WebRequestMethods.Http.Post;

                case OperationType.Update:
                    return MailChimpDefaults.PatchRequestMethod;

                case OperationType.Delete:
                    return MailChimpDefaults.DeleteRequestMethod;

                case OperationType.CreateOrUpdate:
                    return WebRequestMethods.Http.Put;

                default:
                    return WebRequestMethods.Http.Get;
            }
        }

        /// <summary>
        /// Log result of the synchronization
        /// </summary>
        /// <param name="batchId">Batch identifier</param>
        /// <returns>The asynchronous task whose result contains number of completed operations</returns>
        private async Task<int?> LogSynchronizationResult(string batchId)
        {
            return await HandleRequest<int?>(async () =>
            {
                //try to get finished batch of operations
                var batch = await _mailChimpManager.Batches.GetBatchStatus(batchId)
                    ?? throw new NopException("No response from the service");

                var completeStatus = "finished";
                if (!batch?.Status?.Equals(completeStatus) ?? true)
                    return null;

                var operationResults = new List<OperationResult>();
                if (!string.IsNullOrEmpty(batch.ResponseBodyUrl))
                {
                    //get additional result info from MailChimp servers
                    var webResponse = await WebRequest.Create(batch.ResponseBodyUrl).GetResponseAsync();
                    using (var stream = webResponse.GetResponseStream())
                    {
                        //operation results represent a gzipped tar archive of JSON files, so extract it
                        using (var archiveReader = ReaderFactory.Open(stream))
                        {
                            while (archiveReader.MoveToNextEntry())
                            {
                                if (!archiveReader.Entry.IsDirectory)
                                {
                                    using (var unzippedEntryStream = archiveReader.OpenEntryStream())
                                    {
                                        using (var entryReader = new StreamReader(unzippedEntryStream))
                                        {
                                            var entryText = entryReader.ReadToEnd();
                                            operationResults.AddRange(JsonConvert.DeserializeObject<IEnumerable<OperationResult>>(entryText));
                                        }
                                    }
                                }
                            }
                        }
                    }

                }

                //log info
                var message = new StringBuilder();
                message.AppendLine("MailChimp info.");
                message.AppendLine($"Synchronization started at: {batch.SubmittedAt}");
                message.AppendLine($"completed at: {batch.CompletedAt}");
                message.AppendLine($"finished operations: {batch.FinishedOperations}");
                message.AppendLine($"errored operations: {batch.ErroredOperations}");
                message.AppendLine($"total operations: {batch.TotalOperations}");
                message.AppendLine($"batch ID: {batch.Id}");

                //whether there are errors in operation results
                var operationResultsWithErrors = operationResults
                    .Where(result => !int.TryParse(result.StatusCode, out int statusCode) || statusCode != (int)HttpStatusCode.OK);
                if (operationResultsWithErrors.Any())
                {
                    message.AppendLine("Synchronization errors:");
                    foreach (var operationResult in operationResultsWithErrors)
                    {
                        var errorInfo = JsonConvert.DeserializeObject<mailchimp.MailChimpApiError>(operationResult.ResponseString, new JsonSerializerSettings
                        {
                            Error = (sender, args) => { args.ErrorContext.Handled = true; }
                        });

                        var errorMessage = $"Operation {operationResult.OperationId}";
                        if (errorInfo.Errors?.Any() ?? false)
                        {
                            var errorDetails = errorInfo.Errors
                                .Aggregate(string.Empty, (error, detail) => $"{error}{detail?.Field} - {detail?.Message};");
                            errorMessage = $"{errorMessage} - {errorDetails}";
                        }
                        else
                            errorMessage = $"{errorMessage} - {errorInfo.Detail}";

                        message.AppendLine(errorMessage);
                    }
                }

                _logger.Information(message.ToString());

                return batch.TotalOperations;
            });
        }

        #region Subscriptions

        /// <summary>
        /// Get operations to manage subscriptions
        /// </summary>
        /// <returns>List of operations</returns>
        private IList<Operation> GetSubscriptionsOperations()
        {
            var operations = new List<Operation>();

            //prepare operations
            operations.AddRange(GetCreateOrUpdateSubscriptionsOperations());
            operations.AddRange(GetDeleteSubscriptionsOperations());

            return operations;
        }

        /// <summary>
        /// Get operations to create and update subscriptions
        /// </summary>
        /// <returns>List of operations</returns>
        private IList<Operation> GetCreateOrUpdateSubscriptionsOperations()
        {
            var operations = new List<Operation>();

            //get created and updated subscriptions
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Subscription, OperationType.Create).ToList();
            records.AddRange(_synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Subscription, OperationType.Update));
            var subscriptions = records.Distinct().Select(record => _newsLetterSubscriptionService.GetNewsLetterSubscriptionById(record.EntityId));

            foreach (var store in _storeService.GetAllStores())
            {
                //try to get list ID for the store
                var listId = _settingService
                    .GetSettingByKey<string>($"{nameof(MailChimpSettings)}.{nameof(MailChimpSettings.ListId)}", storeId: store.Id, loadSharedValueIfNotFound: true);
                if (string.IsNullOrEmpty(listId))
                    continue;

                //filter subscriptions by store
                var storeSubscriptions = subscriptions.Where(subscription => subscription?.StoreId == store.Id);

                foreach (var subscription in storeSubscriptions)
                {
                    var member = CreateMemberBySubscription(subscription);
                    if (member == null)
                        continue;

                    //create hash by email
                    var hash = _mailChimpManager.Members.Hash(subscription.Email);

                    //prepare request path and operation ID
                    var requestPath = string.Format(MailChimpDefaults.MembersApiPath, listId, hash);
                    var operationId = $"createOrUpdate-subscription-{subscription.Id}-list-{listId}";

                    //add operation
                    operations.Add(CreateOperation(member, OperationType.CreateOrUpdate, requestPath, operationId));
                }
            }

            return operations;
        }

        /// <summary>
        /// Get operations to delete subscriptions
        /// </summary>
        /// <returns>List of operations</returns>
        private IList<Operation> GetDeleteSubscriptionsOperations()
        {
            var operations = new List<Operation>();

            //ge records of deleted subscriptions
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Subscription, OperationType.Delete);

            foreach (var store in _storeService.GetAllStores())
            {
                //try to get list ID for the store
                var listId = _settingService
                    .GetSettingByKey<string>($"{nameof(MailChimpSettings)}.{nameof(MailChimpSettings.ListId)}", storeId: store.Id, loadSharedValueIfNotFound: true);
                if (string.IsNullOrEmpty(listId))
                    continue;

                foreach (var record in records)
                {
                    //if subscription still exist, don't delete it from MailChimp
                    var subscription = _newsLetterSubscriptionService.GetNewsLetterSubscriptionByEmailAndStoreId(record.Email, store.Id);
                    if (subscription != null)
                        continue;

                    //create hash by email
                    var hash = _mailChimpManager.Members.Hash(record.Email);

                    //prepare request path and operation ID
                    var requestPath = string.Format(MailChimpDefaults.MembersApiPath, listId, hash);
                    var operationId = $"delete-subscription-{record.EntityId}-list-{listId}";

                    //add operation
                    operations.Add(CreateOperation<mailchimp.Member>(null, OperationType.Delete, requestPath, operationId));
                }
            }

            return operations;
        }

        /// <summary>
        /// Create MailChimp member object by nopCommerce newsletter subscription object
        /// </summary>
        /// <param name="subscription">Newsletter subscription</param>
        /// <returns>Member</returns>
        private mailchimp.Member CreateMemberBySubscription(NewsLetterSubscription subscription)
        {
            //whether email exists
            if (string.IsNullOrEmpty(subscription?.Email))
                return null;

            var member = new mailchimp.Member
            {
                EmailAddress = subscription.Email,
                TimestampSignup = subscription.CreatedOnUtc.ToString("s")
            };

            //set member status
            var status = subscription.Active ? mailchimp.Status.Subscribed : mailchimp.Status.Unsubscribed;
            member.Status = status;
            member.StatusIfNew = status;

            //if a customer of the subscription isn't a quest, add some specific properties
            var customer = _customerService.GetCustomerByEmail(subscription.Email);
            if (!customer?.IsGuest() ?? false)
            {
                //try to add language
                var languageId = _genericAttributeService.GetAttribute<int>(customer, NopCustomerDefaults.LanguageIdAttribute);
                if (languageId > 0)
                    member.Language = _languageService.GetLanguageById(languageId)?.UniqueSeoCode;

                //try to add names
                var firstName = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.FirstNameAttribute);
                var lastName = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.LastNameAttribute);
                if (!string.IsNullOrEmpty(firstName) || !string.IsNullOrEmpty(lastName))
                {
                    member.MergeFields = new Dictionary<string, object>
                    {
                        [MailChimpDefaults.FirstNameMergeField] = firstName,
                        [MailChimpDefaults.LastNameMergeField] = lastName
                    };
                }
            }

            return member;
        }

        #endregion

        #region E-Commerce data

        /// <summary>
        /// Get operations to manage E-Commerce data
        /// </summary>
        /// <returns>The asynchronous task whose result contains the list of operations</returns>
        private async Task<IList<Operation>> GetEcommerceApiOperations()
        {
            var operations = new List<Operation>();

            //prepare operations
            operations.AddRange(await GetStoreOperations());
            operations.AddRange(GetCustomerOperations());
            operations.AddRange(GetProductOperations());
            operations.AddRange(GetProductVariantOperations());
            operations.AddRange(GetOrderOperations());
            operations.AddRange(await GetCartOperations());

            return operations;
        }

        /// <summary>
        /// Get code of the primary store currency
        /// </summary>
        /// <returns>Currency code</returns>
        private CurrencyCode GetCurrencyCode()
        {
            var currencyCode = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode;
            if (!Enum.TryParse(currencyCode, true, out CurrencyCode result))
                result = CurrencyCode.USD;

            return result;
        }

        #region Stores

        /// <summary>
        /// Get operations to manage stores
        /// </summary>
        /// <returns>The asynchronous task whose result contains the list of operations</returns>
        private async Task<IList<Operation>> GetStoreOperations()
        {
            //first create stores, we don't use batch operations, coz the store is the root object for all E-Commerce data 
            //and we need to make sure that it is created
            await CreateStores();

            var operations = new List<Operation>();

            //prepare operations
            operations.AddRange(GetUpdateStoresOperations());
            operations.AddRange(GetDeleteStoresOperations());

            return operations;
        }

        /// <summary>
        /// Create stores
        /// </summary>
        /// <returns>The asynchronous task whose result determines whether stores successfully created</returns>
        private async Task<bool> CreateStores()
        {
            return await HandleRequest(async () =>
            {
                //get created stores
                var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Store, OperationType.Create);
                var stores = records.Select(record => _storeService.GetStoreById(record.EntityId));

                foreach (var store in stores)
                {
                    var storeObject = MapStore(store);
                    if (storeObject == null)
                        continue;

                    //create store
                    await HandleRequest(async () => await _mailChimpManager.ECommerceStores.AddAsync(storeObject));
                }

                return true;
            });
        }

        /// <summary>
        /// Get operations to update stores
        /// </summary>
        /// <returns>List of operations</returns>
        private IEnumerable<Operation> GetUpdateStoresOperations()
        {
            var operations = new List<Operation>();

            //get updated stores
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Store, OperationType.Update);
            var stores = records.Select(record => _storeService.GetStoreById(record.EntityId));

            foreach (var store in stores)
            {
                var storeObject = MapStore(store);
                if (storeObject == null)
                    continue;

                //prepare request path and operation ID
                var storeId = string.Format(_mailChimpSettings.StoreIdMask, store.Id);
                var requestPath = string.Format(MailChimpDefaults.StoresApiPath, storeId);
                var operationId = $"update-store-{store.Id}";

                //add operation
                operations.Add(CreateOperation(storeObject, OperationType.Update, requestPath, operationId));
            }

            return operations;
        }

        /// <summary>
        /// Get operations to delete stores
        /// </summary>
        /// <returns>List of operations</returns>
        private IEnumerable<Operation> GetDeleteStoresOperations()
        {
            var operations = new List<Operation>();

            //get records of deleted stores
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Store, OperationType.Delete);

            //add operations
            operations.AddRange(records.Select(record =>
            {
                //prepare request path and operation ID
                var storeId = string.Format(_mailChimpSettings.StoreIdMask, record.EntityId);
                var requestPath = string.Format(MailChimpDefaults.StoresApiPath, storeId);
                var operationId = $"delete-store-{record.EntityId}";

                return CreateOperation<mailchimp.Store>(null, OperationType.Delete, requestPath, operationId);
            }));

            return operations;
        }

        /// <summary>
        /// Create MailChimp store object by nopCommerce store object
        /// </summary>
        /// <param name="store">Store</param>
        /// <returns>Store</returns>
        private mailchimp.Store MapStore(Store store)
        {
            return store == null ? null : new mailchimp.Store
            {
                Id = string.Format(_mailChimpSettings.StoreIdMask, store.Id),
                ListId = _settingService
                    .GetSettingByKey<string>($"{nameof(MailChimpSettings)}.{nameof(MailChimpSettings.ListId)}", storeId: store.Id, loadSharedValueIfNotFound: true),
                Name = store.Name,
                Domain = _webHelper.GetStoreLocation(),
                CurrencyCode = GetCurrencyCode(),
                PrimaryLocale = (_languageService.GetLanguageById(store.DefaultLanguageId) ?? _languageService.GetAllLanguages().FirstOrDefault())?.UniqueSeoCode,
                Phone = store.CompanyPhoneNumber,
                Timezone = _dateTimeHelper.DefaultStoreTimeZone?.StandardName
            };
        }

        #endregion

        #region Customers

        /// <summary>
        /// Get operations to manage customers
        /// </summary>
        /// <returns>List of operations</returns>
        private IList<Operation> GetCustomerOperations()
        {
            var operations = new List<Operation>();

            //prepare operations
            operations.AddRange(GetCreateOrUpdateCustomersOperations());
            operations.AddRange(GetDeleteCustomersOperations());

            return operations;
        }

        /// <summary>
        /// Get operations to create and update customers
        /// </summary>
        /// <returns>List of operations</returns>
        private IList<Operation> GetCreateOrUpdateCustomersOperations()
        {
            var operations = new List<Operation>();

            //get created and updated customers
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Customer, OperationType.Create).ToList();
            records.AddRange(_synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Customer, OperationType.Update));
            var customers = _customerService.GetCustomersByIds(records.Select(record => record.EntityId).Distinct().ToArray());

            foreach (var store in _storeService.GetAllStores())
            {
                //create customers for all stores
                foreach (var customer in customers)
                {
                    var customerObject = MapCustomer(customer, store.Id);
                    if (customerObject == null)
                        continue;

                    //prepare request path and operation ID
                    var storeId = string.Format(_mailChimpSettings.StoreIdMask, store.Id);
                    var requestPath = string.Format(MailChimpDefaults.CustomersApiPath, storeId, customer.Id);
                    var operationId = $"createOrUpdate-customer-{customer.Id}-store-{store.Id}";

                    //add operation
                    operations.Add(CreateOperation(customerObject, OperationType.CreateOrUpdate, requestPath, operationId));
                }
            }

            return operations;
        }

        /// <summary>
        /// Get operations to delete customers
        /// </summary>
        /// <returns>List of operations</returns>
        private IList<Operation> GetDeleteCustomersOperations()
        {
            var operations = new List<Operation>();

            //get records of deleted customers
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Customer, OperationType.Delete);

            //add operations
            operations.AddRange(_storeService.GetAllStores().SelectMany(store => records.Select(record =>
            {
                //prepare request path and operation ID
                var storeId = string.Format(_mailChimpSettings.StoreIdMask, store.Id);
                var requestPath = string.Format(MailChimpDefaults.CustomersApiPath, storeId, record.EntityId);
                var operationId = $"delete-customer-{record.EntityId}-store-{store.Id}";

                return CreateOperation<mailchimp.Customer>(null, OperationType.Delete, requestPath, operationId);
            })));

            return operations;
        }

        /// <summary>
        /// Create MailChimp customer object by nopCommerce customer object
        /// </summary>
        /// <param name="customer">Customer</param>
        /// <param name="storeId">Store identifier</param>
        /// <returns>Customer</returns>
        private mailchimp.Customer MapCustomer(Customer customer, int storeId)
        {
            if (customer == null)
                return null;

            //get all customer orders
            var customerOrders = _orderService.SearchOrders(storeId: storeId, customerId: customer.Id).ToList();

            //get customer country and region
            var customerCountry = _countryService.GetCountryById(_genericAttributeService.GetAttribute<int>(customer, NopCustomerDefaults.CountryIdAttribute));
            var customerProvince = _stateProvinceService.GetStateProvinceById(_genericAttributeService.GetAttribute<int>(customer, NopCustomerDefaults.StateProvinceIdAttribute));

            return new mailchimp.Customer
            {
                Id = customer.Id.ToString(),
                EmailAddress = customer.Email,
                OptInStatus = false,
                OrdersCount = customerOrders.Count,
                TotalSpent = (double)customerOrders.Sum(order => order.OrderTotal),
                FirstName = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.FirstNameAttribute),
                LastName = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.LastNameAttribute),
                Company = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.CompanyAttribute),
                Address = new mailchimp.Address
                {
                    Address1 = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.StreetAddressAttribute),
                    Address2 = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.StreetAddress2Attribute),
                    City = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.CityAttribute),
                    Province = customerProvince?.Name,
                    ProvinceCode = customerProvince?.Abbreviation,
                    Country = customerCountry?.Name,
                    CountryCode = customerCountry?.TwoLetterIsoCode,
                    PostalCode = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.ZipPostalCodeAttribute)
                }
            };
        }

        #endregion

        #region Products

        /// <summary>
        /// Get operations to manage products
        /// </summary>
        /// <returns>List of operations</returns>
        private IList<Operation> GetProductOperations()
        {
            var operations = new List<Operation>();

            //prepare operations
            operations.AddRange(GetCreateProductsOperations());
            operations.AddRange(GetUpdateProductsOperations());
            operations.AddRange(GetDeleteProductsOperations());

            return operations;
        }

        /// <summary>
        /// Get operations to create products
        /// </summary>
        /// <returns>List of operations</returns>
        private IList<Operation> GetCreateProductsOperations()
        {
            var operations = new List<Operation>();

            //get created products
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Product, OperationType.Create);
            var products = _productService.GetProductsByIds(records.Select(record => record.EntityId).ToArray());

            foreach (var store in _storeService.GetAllStores())
            {
                //filter products by the store
                var storeProducts = products.Where(product => _storeMappingService.Authorize(product, store.Id));

                foreach (var product in storeProducts)
                {
                    var productObject = MapProduct(product);
                    if (productObject == null)
                        continue;

                    //prepare request path and operation ID
                    var storeId = string.Format(_mailChimpSettings.StoreIdMask, store.Id);
                    var requestPath = string.Format(MailChimpDefaults.ProductsApiPath, storeId, string.Empty);
                    var operationId = $"create-product-{product.Id}-store-{store.Id}";

                    //add operation
                    operations.Add(CreateOperation(productObject, OperationType.Create, requestPath, operationId));
                }
            }

            return operations;
        }

        /// <summary>
        /// Get operations to update products
        /// </summary>
        /// <returns>List of operations</returns>
        private IList<Operation> GetUpdateProductsOperations()
        {
            var operations = new List<Operation>();

            //get updated products
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Product, OperationType.Update);
            var products = _productService.GetProductsByIds(records.Select(record => record.EntityId).ToArray());

            foreach (var store in _storeService.GetAllStores())
            {
                //filter products by the store
                var storeProducts = products.Where(product => _storeMappingService.Authorize(product, store.Id));

                foreach (var product in storeProducts)
                {
                    var productObject = MapProduct(product);
                    if (productObject == null)
                        continue;

                    //prepare request path and operation ID
                    var storeId = string.Format(_mailChimpSettings.StoreIdMask, store.Id);
                    var requestPath = string.Format(MailChimpDefaults.ProductsApiPath, storeId, product.Id);
                    var operationId = $"update-product-{product.Id}-store-{store.Id}";

                    //add operation
                    operations.Add(CreateOperation(productObject, OperationType.Update, requestPath, operationId));

                    //add operation to update default product variant
                    var productVariant = CreateDefaultProductVariantByProduct(product);
                    if (productVariant == null)
                        continue;

                    var requestPathVariant = string.Format(MailChimpDefaults.ProductVariantsApiPath, storeId, product.Id, Guid.Empty.ToString());
                    var operationIdVariant = $"update-productVariant-{Guid.Empty.ToString()}-product-{product.Id}-store-{store.Id}";
                    operations.Add(CreateOperation(productVariant, OperationType.Update, requestPathVariant, operationIdVariant));
                }
            }

            return operations;
        }

        /// <summary>
        /// Get operations to delete products
        /// </summary>
        /// <returns>List of operations</returns>
        private IList<Operation> GetDeleteProductsOperations()
        {
            var operations = new List<Operation>();

            //get records of deleted products
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Product, OperationType.Delete);

            //add operations
            operations.AddRange(_storeService.GetAllStores().SelectMany(store => records.Select(record =>
            {
                //prepare request path and operation ID
                var storeId = string.Format(_mailChimpSettings.StoreIdMask, store.Id);
                var requestPath = string.Format(MailChimpDefaults.ProductsApiPath, storeId, record.EntityId);
                var operationId = $"delete-product-{record.EntityId}-store-{store.Id}";

                return CreateOperation<mailchimp.Product>(null, OperationType.Delete, requestPath, operationId);
            })));

            return operations;
        }

        /// <summary>
        /// Create MailChimp product object by nopCommerce product object
        /// </summary>
        /// <param name="product">Product</param>
        /// <returns>Product</returns>
        private mailchimp.Product MapProduct(Product product)
        {
            return product == null ? null : new mailchimp.Product
            {
                Id = product.Id.ToString(),
                Title = product.Name,
                Url = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext)
                    .RouteUrl(nameof(Product), new { SeName = _urlRecordService.GetSeName(product) }, _actionContextAccessor.ActionContext.HttpContext.Request.Scheme),
                Description = HtmlHelper.StripTags(!string.IsNullOrEmpty(product.FullDescription) ? product.FullDescription :
                    !string.IsNullOrEmpty(product.ShortDescription) ? product.ShortDescription : product.Name),
                Type = product.ProductCategories.FirstOrDefault()?.Category?.Name,
                Vendor = product.ProductManufacturers.FirstOrDefault()?.Manufacturer?.Name,
                ImageUrl = _pictureService.GetPictureUrl(_pictureService.GetProductPicture(product, null)),
                Variants = CreateProductVariantsByProduct(product)
            };
        }

        /// <summary>
        /// Create MailChimp product variant objects by nopCommerce product object
        /// </summary>
        /// <param name="product">Product</param>
        /// <returns>List of product variants</returns>
        private IList<mailchimp.Variant> CreateProductVariantsByProduct(Product product)
        {
            var variants = new List<mailchimp.Variant>();

            //add default variant
            variants.Add(CreateDefaultProductVariantByProduct(product));

            //add variants from attribute combinations
            var combinationVariants = product.ProductAttributeCombinations
                .Where(combination => combination?.Product != null)
                .Select(combination => CreateProductVariantByAttributeCombination(combination));
            variants.AddRange(combinationVariants);

            return variants;
        }

        /// <summary>
        /// Create MailChimp product variant object by nopCommerce product object
        /// </summary>
        /// <param name="product">Product</param>
        /// <returns>Product variant</returns>
        private mailchimp.Variant CreateDefaultProductVariantByProduct(Product product)
        {
            return product == null ? null : new mailchimp.Variant
            {
                Id = Guid.Empty.ToString(), //set empty guid as identifier for default product variant
                Title = product.Name,
                Url = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext)
                    .RouteUrl(nameof(Product), new { SeName = _urlRecordService.GetSeName(product) }, _actionContextAccessor.ActionContext.HttpContext.Request.Scheme),
                Sku = product.Sku,
                Price = (double)product.Price,
                ImageUrl = _pictureService.GetPictureUrl(_pictureService.GetProductPicture(product, null)),
                InventoryQuantity = product.ManageInventoryMethod != ManageInventoryMethod.DontManageStock ? product.StockQuantity : int.MaxValue,
                Visibility = product.Published.ToString().ToLower()
            };
        }

        /// <summary>
        /// Create MailChimp product variant object by nopCommerce product attribute combination object
        /// </summary>
        /// <param name="combination">Product attribute combination</param>
        /// <returns>Product variant</returns>
        private mailchimp.Variant CreateProductVariantByAttributeCombination(ProductAttributeCombination combination)
        {
            return combination?.Product == null ? null : new mailchimp.Variant
            {
                Id = combination.Id.ToString(),
                Title = combination.Product.Name,
                Url = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext)
                    .RouteUrl(nameof(Product), new { SeName = _urlRecordService.GetSeName(combination.Product) }, _actionContextAccessor.ActionContext.HttpContext.Request.Scheme),
                Sku = !string.IsNullOrEmpty(combination.Sku) ? combination.Sku : combination.Product.Sku,
                Price = (double)(combination.OverriddenPrice ?? combination.Product.Price),
                InventoryQuantity = combination.Product.ManageInventoryMethod == ManageInventoryMethod.ManageStockByAttributes
                    ? combination.StockQuantity : combination.Product.ManageInventoryMethod != ManageInventoryMethod.DontManageStock
                    ? combination.Product.StockQuantity : int.MaxValue,
                ImageUrl = _pictureService
                    .GetPictureUrl(_pictureService.GetProductPicture(combination.Product, combination.AttributesXml)),
                Visibility = combination.Product.Published.ToString().ToLowerInvariant()
            };
        }

        /// <summary>
        /// Get operations to manage product variants
        /// </summary>
        /// <returns>List of operations</returns>
        private IList<Operation> GetProductVariantOperations()
        {
            var operations = new List<Operation>();

            //prepare operations
            operations.AddRange(GetCreateOrUpdateProductVariantsOperations());
            operations.AddRange(GetDeleteProductVariantsOperations());

            return operations;
        }

        /// <summary>
        /// Get operations to create and update product variants
        /// </summary>
        /// <returns>List of operations</returns>
        private IList<Operation> GetCreateOrUpdateProductVariantsOperations()
        {
            var operations = new List<Operation>();

            //get created and updated product combinations
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.AttributeCombination, OperationType.Create).ToList();
            records.AddRange(_synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.AttributeCombination, OperationType.Update));
            var combinations = records.Distinct().Select(record => _productAttributeService.GetProductAttributeCombinationById(record.EntityId));

            foreach (var store in _storeService.GetAllStores())
            {
                //filter combinations by the store
                var storeCombinations = combinations.Where(combination => _storeMappingService.Authorize(combination?.Product, store.Id));

                foreach (var combination in storeCombinations)
                {
                    var productVariant = CreateProductVariantByAttributeCombination(combination);
                    if (productVariant == null)
                        continue;

                    //prepare request path and operation ID
                    var storeId = string.Format(_mailChimpSettings.StoreIdMask, store.Id);
                    var requestPath = string.Format(MailChimpDefaults.ProductVariantsApiPath, storeId, combination.ProductId, combination.Id);
                    var operationId = $"createOrUpdate-productVariant-{combination.Id}-product-{combination.ProductId}-store-{store.Id}";

                    //add operation
                    operations.Add(CreateOperation(productVariant, OperationType.CreateOrUpdate, requestPath, operationId));
                }
            }

            return operations;
        }

        /// <summary>
        /// Get operations to delete product variants
        /// </summary>
        /// <returns>List of operations</returns>
        private IList<Operation> GetDeleteProductVariantsOperations()
        {
            var operations = new List<Operation>();

            //get records of deleted product combinations
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.AttributeCombination, OperationType.Delete);

            //add operations
            operations.AddRange(_storeService.GetAllStores().SelectMany(store => records.Select(record =>
            {
                //prepare request path and operation ID
                var storeId = string.Format(_mailChimpSettings.StoreIdMask, store.Id);
                var requestPath = string.Format(MailChimpDefaults.ProductVariantsApiPath, storeId, record.ProductId, record.EntityId);
                var operationId = $"delete-productVariant-{record.EntityId}-product-{record.ProductId}-store-{store.Id}";

                return CreateOperation<mailchimp.Variant>(null, OperationType.Delete, requestPath, operationId);
            })));

            return operations;
        }

        #endregion

        #region Orders

        /// <summary>
        /// Get operations to manage orders
        /// </summary>
        /// <returns>List of operations</returns>
        private IList<Operation> GetOrderOperations()
        {
            var operations = new List<Operation>();

            //prepare operations
            operations.AddRange(GetCreateOrdersOperations());
            operations.AddRange(GetUpdateOrdersOperations());
            operations.AddRange(GetDeleteOrdersOperations());

            return operations;
        }

        /// <summary>
        /// Get operations to create orders
        /// </summary>
        /// <returns>List of operations</returns>
        private IList<Operation> GetCreateOrdersOperations()
        {
            var operations = new List<Operation>();

            //get created orders
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Order, OperationType.Create);
            var orders = _orderService.GetOrdersByIds(records.Select(record => record.EntityId).ToArray())
                .Where(order => !order.Customer?.IsGuest() ?? false);

            foreach (var store in _storeService.GetAllStores())
            {
                //filter orders by the store
                var storeOrders = orders.Where(order => order?.StoreId == store.Id);

                foreach (var order in storeOrders)
                {
                    var orderObject = MapOrder(order);
                    if (orderObject == null)
                        continue;

                    //prepare request path and operation ID
                    var storeId = string.Format(_mailChimpSettings.StoreIdMask, store.Id);
                    var requestPath = string.Format(MailChimpDefaults.OrdersApiPath, storeId, string.Empty);
                    var operationId = $"create-order-{order.Id}-store-{store.Id}";

                    //add operation
                    operations.Add(CreateOperation(orderObject, OperationType.Create, requestPath, operationId));
                }
            }

            return operations;
        }

        /// <summary>
        /// Get operations to update orders
        /// </summary>
        /// <returns>List of operations</returns>
        private IList<Operation> GetUpdateOrdersOperations()
        {
            var operations = new List<Operation>();

            //get updated orders
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Order, OperationType.Update);
            var orders = _orderService.GetOrdersByIds(records.Select(record => record.EntityId).ToArray())
                .Where(order => !order.Customer?.IsGuest() ?? false);

            foreach (var store in _storeService.GetAllStores())
            {
                //filter orders by the store
                var storeOrders = orders.Where(order => order?.StoreId == store.Id);

                foreach (var order in storeOrders)
                {
                    var orderObject = MapOrder(order);
                    if (orderObject == null)
                        continue;

                    //prepare request path and operation ID
                    var storeId = string.Format(_mailChimpSettings.StoreIdMask, store.Id);
                    var requestPath = string.Format(MailChimpDefaults.OrdersApiPath, storeId, order.Id);
                    var operationId = $"update-order-{order.Id}-store-{store.Id}";

                    //add operation
                    operations.Add(CreateOperation(orderObject, OperationType.Update, requestPath, operationId));
                }
            }

            return operations;
        }

        /// <summary>
        /// Get operations to delete orders
        /// </summary>
        /// <returns>List of operations</returns>
        private IList<Operation> GetDeleteOrdersOperations()
        {
            var operations = new List<Operation>();

            //get records of deleted orders
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Order, OperationType.Delete);

            //add operations
            operations.AddRange(_storeService.GetAllStores().SelectMany(store => records.Select(record =>
            {
                //prepare request path and operation ID
                var storeId = string.Format(_mailChimpSettings.StoreIdMask, store.Id);
                var requestPath = string.Format(MailChimpDefaults.OrdersApiPath, storeId, record.EntityId);
                var operationId = $"delete-order-{record.EntityId}-store-{store.Id}";

                return CreateOperation<mailchimp.Order>(null, OperationType.Delete, requestPath, operationId);
            })));

            return operations;
        }

        /// <summary>
        /// Create MailChimp order object by nopCommerce order object
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Order</returns>
        private mailchimp.Order MapOrder(Order order)
        {
            return order == null ? null : new mailchimp.Order
            {
                Id = order.Id.ToString(),
                Customer = new mailchimp.Customer { Id = order.Customer.Id.ToString() },
                FinancialStatus = order.PaymentStatus.ToString("D"),
                FulfillmentStatus = order.OrderStatus.ToString("D"),
                CurrencyCode = GetCurrencyCode(),
                OrderTotal = (double)order.OrderTotal,
                TaxTotal = (double)order.OrderTax,
                ShippingTotal = (double)order.OrderShippingInclTax,
                ProcessedAtForeign = order.CreatedOnUtc.ToString("s"),
                ShippingAddress = order.PickUpInStore && order.PickupAddress != null ? MapAddress(order.PickupAddress) : MapAddress(order.ShippingAddress),
                BillingAddress = MapAddress(order.BillingAddress),
                Lines = order.OrderItems.Select(item => MapOrderItem(item)).ToList()
            };
        }

        /// <summary>
        /// Create MailChimp address object by nopCommerce address object
        /// </summary>
        /// <param name="address">Address</param>
        /// <returns>Address</returns>
        private mailchimp.Address MapAddress(Address address)
        {
            return address == null ? null : new mailchimp.Address
            {
                Address1 = address.Address1,
                Address2 = address.Address2,
                City = address.City,
                Province = address.StateProvince?.Name,
                ProvinceCode = address.StateProvince?.Abbreviation,
                Country = address.Country?.Name,
                CountryCode = address.Country?.TwoLetterIsoCode,
                PostalCode = address.ZipPostalCode,
            };
        }

        /// <summary>
        /// Create MailChimp line object by nopCommerce order item object
        /// </summary>
        /// <param name="item">Order item</param>
        /// <returns>Line</returns>
        private mailchimp.Line MapOrderItem(OrderItem item)
        {
            return item?.Product == null ? null : new mailchimp.Line
            {
                Id = item.Id.ToString(),
                ProductId = item.ProductId.ToString(),
                ProductVariantId = _productAttributeParser
                    .FindProductAttributeCombination(item.Product, item.AttributesXml)?.Id.ToString() ?? Guid.Empty.ToString(),
                Price = (double)item.PriceInclTax,
                Quantity = item.Quantity
            };
        }

        #endregion

        #region Carts

        /// <summary>
        /// Get operations to manage carts
        /// </summary>
        /// <returns>The asynchronous task whose result contains the list of operations</returns>
        private async Task<IList<Operation>> GetCartOperations()
        {
            var operations = new List<Operation>();

            //get customers with shopping cart
            var customersWithCart = _customerService.GetAllCustomers(loadOnlyWithShoppingCart: true, sct: ShoppingCartType.ShoppingCart)
                .Where(customer => !customer.IsGuest());

            foreach (var store in _storeService.GetAllStores())
            {
                var storeId = string.Format(_mailChimpSettings.StoreIdMask, store.Id);

                //filter customers with cart by the store
                var storeCustomersWithCart = customersWithCart
                    .Where(customer => customer.ShoppingCartItems.Any(cart => cart?.StoreId == store.Id)).ToList();

                //get existing carts on MailChimp
                var cartsIds = await HandleRequest(async () =>
                {
                    //get number of carts
                    var cartNumber = (await _mailChimpManager.ECommerceStores.Carts(storeId).GetResponseAsync())?.TotalItems
                        ?? throw new NopException("No response from the service");

                    return (await _mailChimpManager.ECommerceStores.Carts(storeId)
                        .GetAllAsync(new QueryableBaseRequest { FieldsToInclude = "carts.id", Limit = cartNumber }))
                        ?.Select(cart => cart.Id).ToList()
                        ?? throw new NopException("No response from the service");
                }) ?? new List<string>();

                //add operations to create carts
                var newCustomersWithCart = storeCustomersWithCart.Where(customer => !cartsIds.Contains(customer.Id.ToString()));
                foreach (var customer in newCustomersWithCart)
                {
                    var cart = CreateCartByCustomer(customer, store.Id);
                    if (cart == null)
                        continue;

                    //prepare request path and operation ID
                    var requestPath = string.Format(MailChimpDefaults.CartsApiPath, storeId, string.Empty);
                    var operationId = $"create-cart-{customer.Id}-store-{store.Id}";

                    //add operation
                    operations.Add(CreateOperation(cart, OperationType.Create, requestPath, operationId));
                }

                //add operations to update carts
                var customersWithUpdatedCart = storeCustomersWithCart.Where(customer => cartsIds.Contains(customer.Id.ToString()));
                foreach (var customer in customersWithUpdatedCart)
                {
                    var cart = CreateCartByCustomer(customer, store.Id);
                    if (cart == null)
                        continue;

                    //prepare request path and operation ID
                    var requestPath = string.Format(MailChimpDefaults.CartsApiPath, storeId, customer.Id);
                    var operationId = $"update-cart-{customer.Id}-store-{store.Id}";

                    //add operation
                    operations.Add(CreateOperation(cart, OperationType.Update, requestPath, operationId));
                }

                //add operations to delete carts
                var customersIdsWithoutCart = cartsIds.Except(storeCustomersWithCart.Select(customer => customer.Id.ToString()));
                operations.AddRange(customersIdsWithoutCart.Select(customerId =>
                {
                    //prepare request path and operation ID
                    var requestPath = string.Format(MailChimpDefaults.CartsApiPath, storeId, customerId);
                    var operationId = $"delete-cart-{customerId}-store-{store.Id}";

                    return CreateOperation<mailchimp.Cart>(null, OperationType.Delete, requestPath, operationId);
                }));
            }

            return operations;
        }

        /// <summary>
        /// Create MailChimp cart object by nopCommerce customer object
        /// </summary>
        /// <param name="customer">Customer</param>
        /// <param name="storeId">Store identifier</param>
        /// <returns>Cart</returns>
        private mailchimp.Cart CreateCartByCustomer(Customer customer, int storeId)
        {
            if (customer == null)
                return null;

            //create cart lines
            var lines = customer.ShoppingCartItems.LimitPerStore(storeId)
                .Select(item => MapShoppingCartItem(item)).Where(line => line != null).ToList();

            return new mailchimp.Cart
            {
                Id = customer.Id.ToString(),
                Customer = new mailchimp.Customer { Id = customer.Id.ToString() },
                CheckoutUrl = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext)
                    .RouteUrl("ShoppingCart", null, _actionContextAccessor.ActionContext.HttpContext.Request.Scheme),
                CurrencyCode = GetCurrencyCode(),
                OrderTotal = lines.Sum(line => line.Price),
                Lines = lines
            };
        }

        /// <summary>
        /// Create MailChimp line object by nopCommerce shopping cart item object
        /// </summary>
        /// <param name="item">Shopping cart item</param>
        /// <returns>Line</returns>
        private mailchimp.Line MapShoppingCartItem(ShoppingCartItem item)
        {
            return item?.Product == null ? null : new mailchimp.Line
            {
                Id = item.Id.ToString(),
                ProductId = item.ProductId.ToString(),
                ProductVariantId = _productAttributeParser
                    .FindProductAttributeCombination(item.Product, item.AttributesXml)?.Id.ToString() ?? Guid.Empty.ToString(),
                Price = (double)_priceCalculationService.GetSubTotal(item),
                Quantity = item.Quantity
            };
        }

        #endregion

        #endregion

        #endregion

        #endregion

        #region Methods

        /// <summary>
        /// Synchronize data with MailChimp
        /// </summary>
        /// <param name="manualSynchronization">Whether it's a manual synchronization</param>
        /// <returns>The asynchronous task whose result contains number of operation to synchronize</returns>
        public async Task<int?> Synchronize(bool manualSynchronization = false)
        {
            return await HandleRequest<int?>(async () =>
            {
                //prepare records to manual synchronization
                if (manualSynchronization)
                {
                    var recordsPrepared = await PrepareRecordsToManualSynchronization();
                    if (!recordsPrepared)
                        return null;
                }

                //prepare batch webhook
                var webhookPrepared = await PrepareBatchWebhook();
                if (!webhookPrepared)
                    return null;

                var operations = new List<Operation>();

                //preare subscription operations
                operations.AddRange(GetSubscriptionsOperations());

                //prepare E-Commerce operations
                if (_mailChimpSettings.PassEcommerceData)
                    operations.AddRange(await GetEcommerceApiOperations());

                //start synchronization
                var batchNumber = operations.Count / _mailChimpSettings.BatchOperationNumber + 
                    (operations.Count % _mailChimpSettings.BatchOperationNumber > 0 ? 1 : 0);
                for (int i = 0; i < batchNumber; i++)
                {
                    var batchOperations = operations.Skip(i * _mailChimpSettings.BatchOperationNumber).Take(_mailChimpSettings.BatchOperationNumber);
                    var batch = await _mailChimpManager.Batches.AddAsync(new BatchRequest { Operations = batchOperations })
                        ?? throw new NopException("No response from the service");
                }

                //synchronization successfully started, thus delete records
                if (_mailChimpSettings.PassEcommerceData)
                    _synchronizationRecordService.ClearRecords();
                else
                    _synchronizationRecordService.DeleteRecordsByEntityType(EntityType.Subscription);

                return operations.Count;
            });
        }

        /// <summary>
        /// Get account information
        /// </summary>
        /// <returns>The asynchronous task whose result contains the account information</returns>
        public async Task<string> GetAccountInfo()
        {
            return await HandleRequest(async () =>
            {
                //get account info
                var apiInfo = await _mailChimpManager.Api.GetInfoAsync()
                    ?? throw new NopException("No response from the service");

                return $"{apiInfo.AccountName}{Environment.NewLine}Total subscribers: {apiInfo.TotalSubscribers}";
            });
        }

        /// <summary>
        /// Get available user lists for the synchronization
        /// </summary>
        /// <returns>The asynchronous task whose result contains the list of user lists</returns>
        public async Task<IList<SelectListItem>> GetAvailableLists()
        {
            return await HandleRequest(async () =>
            {
                //get number of lists
                var listNumber = (await _mailChimpManager.Lists.GetResponseAsync())?.TotalItems
                    ?? throw new NopException("No response from the service");

                //get all available lists
                var availableLists = await _mailChimpManager.Lists.GetAllAsync(new ListRequest { Limit = listNumber })
                    ?? throw new NopException("No response from the service");

                return availableLists.Select(list => new SelectListItem { Text = list.Name, Value = list.Id }).ToList();
            });
        }

        /// <summary>
        /// Prepare webhook for passed list
        /// </summary>
        /// <param name="listId">Current selected list identifier</param>
        /// <returns>The asynchronous task whose result determines whether webhook prepared</returns>
        public async Task<bool> PrepareWebhook(string listId)
        {
            return await HandleRequest(async () =>
            {
                //if list ID is empty, nothing to do
                if (string.IsNullOrEmpty(listId))
                    return true;

                //generate webhook URL
                var webhookUrl = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext)
                    .RouteUrl(MailChimpDefaults.WebhookRoute, null, _actionContextAccessor.ActionContext.HttpContext.Request.Scheme);

                //get current list webhooks 
                var listWebhooks = await _mailChimpManager.WebHooks.GetAllAsync(listId)
                    ?? throw new NopException("No response from the service");

                //create the new one if not exists
                var listWebhook = listWebhooks
                    .FirstOrDefault(webhook => !string.IsNullOrEmpty(webhook.Url) && webhook.Url.Equals(webhookUrl, StringComparison.InvariantCultureIgnoreCase));
                if (string.IsNullOrEmpty(listWebhook?.Id))
                {
                    listWebhook = await _mailChimpManager.WebHooks.AddAsync(listId, new mailchimp.WebHook
                    {
                        Event = new mailchimp.Event { Subscribe = true, Unsubscribe = true, Cleaned = true },
                        ListId = listId,
                        Source = new mailchimp.Source { Admin = true, User = true },
                        Url = webhookUrl
                    }) ?? throw new NopException("No response from the service");
                }

                return true;
            });
        }

        /// <summary>
        /// Delete webhooks
        /// </summary>
        /// <returns>The asynchronous task whose result determines whether webhooks successfully deleted</returns>
        public async Task<bool> DeleteWebhooks()
        {
            return await HandleRequest(async () =>
            {
                //get all account webhooks
                var listNumber = (await _mailChimpManager.Lists.GetResponseAsync())?.TotalItems
                    ?? throw new NopException("No response from the service");

                var allListIds = (await _mailChimpManager.Lists.GetAllAsync(new ListRequest { FieldsToInclude = "lists.id", Limit = listNumber }))
                    ?.Select(list => list.Id).ToList()
                    ?? throw new NopException("No response from the service");

                var allWebhooks = (await Task.WhenAll(allListIds.Select(listId => _mailChimpManager.WebHooks.GetAllAsync(listId))))
                    .SelectMany(webhook => webhook).ToList();

                //generate webhook URL
                var webhookUrl = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext)
                    .RouteUrl(MailChimpDefaults.WebhookRoute, null, _actionContextAccessor.ActionContext.HttpContext.Request.Scheme);

                //delete all webhook with matched URL
                var webhooksToDelete = allWebhooks.Where(webhook => webhook.Url.Equals(webhookUrl, StringComparison.InvariantCultureIgnoreCase));
                foreach (var webhook in webhooksToDelete)
                {
                    await HandleRequest(async () =>
                    {
                        await _mailChimpManager.WebHooks.DeleteAsync(webhook.ListId, webhook.Id);
                        return true;
                    });
                }

                return true;
            });
        }

        /// <summary>
        /// Delete batch webhook
        /// </summary>
        /// <returns>The asynchronous task whose result determines whether the webhook successfully deleted</returns>
        public async Task<bool> DeleteBatchWebhook()
        {
            return await HandleRequest(async () =>
            {
                //get all batch webhooks 
                var allBatchWebhooks = await _mailChimpManager.BatchWebHooks.GetAllAsync(new QueryableBaseRequest { Limit = int.MaxValue })
                    ?? throw new NopException("No response from the service");

                //generate webhook URL
                var webhookUrl = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext)
                    .RouteUrl(MailChimpDefaults.BatchWebhookRoute, null, _actionContextAccessor.ActionContext.HttpContext.Request.Scheme);

                //delete webhook if exists
                var batchWebhook = allBatchWebhooks
                    .FirstOrDefault(webhook => webhook.Url.Equals(webhookUrl, StringComparison.InvariantCultureIgnoreCase));

                if (!string.IsNullOrEmpty(batchWebhook?.Id))
                    await _mailChimpManager.BatchWebHooks.DeleteAsync(batchWebhook.Id);

                return true;
            });
        }

        /// <summary>
        /// Handle batch webhook
        /// </summary>
        /// <param name="form">Request form parameters</param>
        /// <param name="handledBatchesInfo">Already handled batches info</param>
        /// <returns>The asynchronous task whose result contains batch identifier and number of completed operations</returns>
        public async Task<(string Id, int? CompletedOperationNumber)> HandleBatchWebhook(IFormCollection form, IDictionary<string, int> handledBatchesInfo)
        {
            return await HandleRequest<(string, int?)>(async () =>
            {
                var batchWebhookType = "batch_operation_completed";
                if (!form.TryGetValue("type", out StringValues webhookType) || !webhookType.Equals(batchWebhookType))
                    return (null, null);

                var completeStatus = "finished";
                if (!form.TryGetValue("data[status]", out StringValues batchStatus) || !batchStatus.Equals(completeStatus))
                    return (null, null);

                if (!form.TryGetValue("data[id]", out StringValues batchId))
                    return (null, null);

                //ensure that this batch is not yet handled
                var alreadyHandledBatchInfo = handledBatchesInfo.FirstOrDefault(batchInfo => batchInfo.Key.Equals(batchId));
                if (!alreadyHandledBatchInfo.Equals(default(KeyValuePair<string, int>)))
                    return (alreadyHandledBatchInfo.Key, alreadyHandledBatchInfo.Value);
                
                //log and return results
                var completedOperationNumber = await LogSynchronizationResult(batchId);

                return (batchId, completedOperationNumber);
            });
        }

        /// <summary>
        /// Handle webhook
        /// </summary>
        /// <param name="form">Request form parameters</param>
        /// <returns>The asynchronous task whose result determines whether the webhook successfully handled</returns>
        public async Task<bool> HandleWebhook(IFormCollection form)
        {
            return await HandleRequest(async () =>
            {
                //try to get subscriber list identifier
                if (!form.TryGetValue("data[list_id]", out StringValues listId))
                    return false;

                //get stores that tied to a specific MailChimp list
                var settingsName = $"{nameof(MailChimpSettings)}.{nameof(MailChimpSettings.ListId)}";
                var storeIds = _storeService.GetAllStores()
                    .Where(store => listId.Equals(_settingService.GetSettingByKey<string>(settingsName, storeId: store.Id, loadSharedValueIfNotFound: true)))
                    .Select(store => store.Id).ToList();

                if (!form.TryGetValue("data[email]", out StringValues email))
                    return false;

                if (!form.TryGetValue("type", out StringValues webhookType))
                    return false;

                //deactivate subscriptions
                var unsubscribeType = "unsubscribe";
                var cleanedType = "cleaned";
                if (webhookType.Equals(unsubscribeType) || webhookType.Equals(cleanedType))
                {
                    //get existing subscriptions by email
                    var subscriptions = storeIds
                        .Select(storeId => _newsLetterSubscriptionService.GetNewsLetterSubscriptionByEmailAndStoreId(email, storeId))
                        .Where(subscription => !string.IsNullOrEmpty(subscription?.Email)).ToList();

                    foreach (var subscription in subscriptions)
                    {
                        //deactivate
                        subscription.Active = false;
                        _newsLetterSubscriptionService.UpdateNewsLetterSubscription(subscription, false);
                        _logger.Information($"MailChimp info. Email {subscription.Email} was unsubscribed from the store #{subscription.StoreId}");
                    }
                }

                //activate subscriptions
                var subscribeType = "subscribe";
                if (webhookType.Equals(subscribeType))
                {
                    foreach (var storeId in storeIds)
                    {
                        var subscription = _newsLetterSubscriptionService.GetNewsLetterSubscriptionByEmailAndStoreId(email, storeId);

                        //if subscription doesn't exist, create the new one
                        if (subscription == null)
                        {
                            _newsLetterSubscriptionService.InsertNewsLetterSubscription(new NewsLetterSubscription
                            {
                                NewsLetterSubscriptionGuid = Guid.NewGuid(),
                                Email = email,
                                StoreId = storeId,
                                Active = true,
                                CreatedOnUtc = DateTime.UtcNow
                            }, false);
                        }
                        else
                        {
                            //or just activate the existing one
                            subscription.Active = true;
                            _newsLetterSubscriptionService.UpdateNewsLetterSubscription(subscription, false);

                        }
                        _logger.Information($"MailChimp info. Email {subscription.Email} has been subscribed to the store #{subscription.StoreId}");
                    }
                }

                return await Task.FromResult(true);
            });
        }

        #endregion
    }
}