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
        private readonly IAddressService _addressService;
        private readonly ICategoryService _categoryService;
        private readonly ICountryService _countryService;
        private readonly ICurrencyService _currencyService;
        private readonly ICustomerService _customerService;
        private readonly IDateTimeHelper _dateTimeHelper;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILanguageService _languageService;
        private readonly ILogger _logger;
        private readonly IMailChimpManager _mailChimpManager;
        private readonly IManufacturerService _manufacturerService;
        private readonly INewsLetterSubscriptionService _newsLetterSubscriptionService;
        private readonly IOrderService _orderService;
        private readonly IPictureService _pictureService;
        private readonly IPriceCalculationService _priceCalculationService;
        private readonly IProductAttributeParser _productAttributeParser;
        private readonly IProductAttributeService _productAttributeService;
        private readonly IProductService _productService;
        private readonly ISettingService _settingService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly IStoreMappingService _storeMappingService;
        private readonly IStoreService _storeService;
        private readonly ISynchronizationRecordService _synchronizationRecordService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IUrlRecordService _urlRecordService;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly MailChimpSettings _mailChimpSettings;

        #endregion

        #region Ctor

        public MailChimpManager(CurrencySettings currencySettings,
            IActionContextAccessor actionContextAccessor,
            IAddressService addressService,
            ICategoryService categoryService,
            ICountryService countryService,
            ICurrencyService currencyService,
            ICustomerService customerService,
            IDateTimeHelper dateTimeHelper,
            ILanguageService languageService,
            ILogger logger,
            IManufacturerService manufacturerService,
            INewsLetterSubscriptionService newsLetterSubscriptionService,
            IOrderService orderService,
            IPictureService pictureService,
            IPriceCalculationService priceCalculationService,
            IProductAttributeParser productAttributeParser,
            IProductAttributeService productAttributeService,
            IProductService productService,
            ISettingService settingService,
            IShoppingCartService shoppingCartService,
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
            _currencySettings = currencySettings;
            _actionContextAccessor = actionContextAccessor;
            _addressService = addressService;
            _categoryService = categoryService;
            _countryService = countryService;
            _currencyService = currencyService;
            _customerService = customerService;
            _dateTimeHelper = dateTimeHelper;
            _languageService = languageService;
            _logger = logger;
            _manufacturerService = manufacturerService;
            _newsLetterSubscriptionService = newsLetterSubscriptionService;
            _orderService = orderService;
            _pictureService = pictureService;
            _priceCalculationService = priceCalculationService;
            _productAttributeParser = productAttributeParser;
            _productAttributeService = productAttributeService;
            _productService = productService;
            _settingService = settingService;
            _shoppingCartService = shoppingCartService;
            _stateProvinceService = stateProvinceService;
            _storeMappingService = storeMappingService;
            _storeService = storeService;
            _synchronizationRecordService = synchronizationRecordService;
            _urlHelperFactory = urlHelperFactory;
            _webHelper = webHelper;
            _workContext = workContext;
            _genericAttributeService = genericAttributeService;
            _mailChimpSettings = mailChimpSettings;
            _urlRecordService = urlRecordService;

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
        private async Task<T> HandleRequestAsync<T>(Func<Task<T>> request)
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
                await _logger.ErrorAsync($"MailChimp error. {errorMessage}", exception, await _workContext.GetCurrentCustomerAsync());

                return default;
            }
        }

        #region Synchronization

        /// <summary>
        /// Prepare records for the manual synchronization
        /// </summary>
        /// <returns>The asynchronous task whose result determines whether the records prepared</returns>
        private async Task<bool> PrepareRecordsToManualSynchronizationAsync()
        {
            return await HandleRequestAsync(async () =>
            {
                //whether to clear existing E-Commerce data
                if (_mailChimpSettings.PassEcommerceData)
                {
                    //get store identifiers
                    var allStoresIds = (await _storeService.GetAllStoresAsync()).Select(store => string.Format(_mailChimpSettings.StoreIdMask, store.Id));

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
                    await _synchronizationRecordService.ClearRecordsAsync();

                }
                else
                    await _synchronizationRecordService.DeleteRecordsByEntityTypeAsync(EntityType.Subscription);

                //and create initial data
                await CreateInitialDataAsync();

                return true;
            });
        }

        /// <summary>
        /// Create data for the manual synchronization
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        private async Task CreateInitialDataAsync()
        {
            //add all subscriptions
            foreach (var subscription in await _newsLetterSubscriptionService.GetAllNewsLetterSubscriptionsAsync())
            {
                await _synchronizationRecordService.InsertRecordAsync(new MailChimpSynchronizationRecord
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
            foreach (var store in await _storeService.GetAllStoresAsync())
            {
                await _synchronizationRecordService.InsertRecordAsync(new MailChimpSynchronizationRecord
                {
                    EntityType = EntityType.Store,
                    EntityId = store.Id,
                    OperationType = OperationType.Create
                });
            }

            var customers = await (await _customerService.GetAllCustomersAsync()).WhereAwait(async customer => !await _customerService.IsGuestAsync(customer)).ToListAsync();
            //add registered customers
            foreach (var customer in customers)
            {
                await _synchronizationRecordService.InsertRecordAsync(new MailChimpSynchronizationRecord
                {
                    EntityType = EntityType.Customer,
                    EntityId = customer.Id,
                    OperationType = OperationType.Create
                });
            }

            //add products
            foreach (var product in await _productService.SearchProductsAsync())
            {
                await _synchronizationRecordService.InsertRecordAsync(new MailChimpSynchronizationRecord
                {
                    EntityType = EntityType.Product,
                    EntityId = product.Id,
                    OperationType = OperationType.Create
                });
            }

            //add orders
            foreach (var order in await _orderService.SearchOrdersAsync())
            {
                await _synchronizationRecordService.InsertRecordAsync(new MailChimpSynchronizationRecord
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
        private async Task<bool> PrepareBatchWebhookAsync()
        {
            return await HandleRequestAsync(async () =>
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
            return operationType switch
            {
                OperationType.Read => WebRequestMethods.Http.Get,
                OperationType.Create => WebRequestMethods.Http.Post,
                OperationType.Update => MailChimpDefaults.PatchRequestMethod,
                OperationType.Delete => MailChimpDefaults.DeleteRequestMethod,
                OperationType.CreateOrUpdate => WebRequestMethods.Http.Put,
                _ => WebRequestMethods.Http.Get,
            };
        }

        /// <summary>
        /// Log result of the synchronization
        /// </summary>
        /// <param name="batchId">Batch identifier</param>
        /// <returns>The asynchronous task whose result contains number of completed operations</returns>
        private async Task<int?> LogSynchronizationResultAsync(string batchId)
        {
            return await HandleRequestAsync<int?>(async () =>
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
                    using var stream = webResponse.GetResponseStream();
                    //operation results represent a gzipped tar archive of JSON files, so extract it
                    using var archiveReader = ReaderFactory.Open(stream);
                    while (archiveReader.MoveToNextEntry())
                    {
                        if (!archiveReader.Entry.IsDirectory)
                        {
                            using var unzippedEntryStream = archiveReader.OpenEntryStream();
                            using var entryReader = new StreamReader(unzippedEntryStream);
                            var entryText = entryReader.ReadToEnd();
                            operationResults.AddRange(JsonConvert.DeserializeObject<IEnumerable<OperationResult>>(entryText));
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
                message.AppendLine($"batch status: {batch.Status}");

                //whether there are errors in operation results
                var operationResultsWithErrors = operationResults
                    .Where(result => !int.TryParse(result.StatusCode, out var statusCode) || statusCode != (int)HttpStatusCode.OK);
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
                            errorMessage = $"{errorInfo.Type} - {errorInfo.Title} - {errorMessage} - {errorDetails}";
                        }
                        else
                            errorMessage = $"{errorInfo.Type} - {errorInfo.Title} - {errorMessage} - {errorInfo.Detail}";

                        message.AppendLine(errorMessage);
                    }
                }

                await _logger.InformationAsync(message.ToString());

                return batch.TotalOperations;
            });
        }

        #region Subscriptions

        /// <summary>
        /// Get operations to manage subscriptions
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the list of operation
        /// </returns>
        private async Task<IList<Operation>> GetSubscriptionsOperationsAsync()
        {
            var operations = new List<Operation>();

            //prepare operations
            operations.AddRange(await GetCreateOrUpdateSubscriptionsOperationsAsync());
            operations.AddRange(await GetDeleteSubscriptionsOperationsAsync());

            return operations;
        }

        /// <summary>
        /// Get operations to create and update subscriptions
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the list of operation
        /// </returns>
        private async Task<IList<Operation>> GetCreateOrUpdateSubscriptionsOperationsAsync()
        {
            var operations = new List<Operation>();

            //get created and updated subscriptions
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Subscription, OperationType.Create).ToList();
            records.AddRange(_synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Subscription, OperationType.Update));
            var subscriptions = await records.Distinct().SelectAwait(async record => await _newsLetterSubscriptionService.GetNewsLetterSubscriptionByIdAsync(record.EntityId)).ToListAsync();

            foreach (var store in await _storeService.GetAllStoresAsync())
            {
                //try to get list ID for the store
                var listId = await _settingService
                    .GetSettingByKeyAsync<string>($"{nameof(MailChimpSettings)}.{nameof(MailChimpSettings.ListId)}", storeId: store.Id, loadSharedValueIfNotFound: true);
                if (string.IsNullOrEmpty(listId))
                    continue;

                //filter subscriptions by store
                var storeSubscriptions = subscriptions.Where(subscription => subscription?.StoreId == store.Id);

                foreach (var subscription in storeSubscriptions)
                {
                    var member = await CreateMemberBySubscriptionAsync(subscription);
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
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the list of operation
        /// </returns>
        private async Task<IList<Operation>> GetDeleteSubscriptionsOperationsAsync()
        {
            var operations = new List<Operation>();

            //ge records of deleted subscriptions
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Subscription, OperationType.Delete);

            foreach (var store in await _storeService.GetAllStoresAsync())
            {
                //try to get list ID for the store
                var listId = await _settingService
                    .GetSettingByKeyAsync<string>($"{nameof(MailChimpSettings)}.{nameof(MailChimpSettings.ListId)}", storeId: store.Id, loadSharedValueIfNotFound: true);
                if (string.IsNullOrEmpty(listId))
                    continue;

                foreach (var record in records)
                {
                    //if subscription still exist, don't delete it from MailChimp
                    var subscription = await _newsLetterSubscriptionService.GetNewsLetterSubscriptionByEmailAndStoreIdAsync(record.Email, store.Id);
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
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the Member
        /// </returns>
        private async Task<mailchimp.Member> CreateMemberBySubscriptionAsync(NewsLetterSubscription subscription)
        {
            //whether email exists
            if (string.IsNullOrEmpty(subscription?.Email))
                return null;

            var member = new mailchimp.Member
            {
                EmailAddress = subscription.Email,
                TimestampSignup = subscription.CreatedOnUtc.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            //set member status
            var status = subscription.Active ? mailchimp.Status.Subscribed : mailchimp.Status.Unsubscribed;
            member.Status = status;
            member.StatusIfNew = status;

            //if a customer of the subscription isn't a guest, add some specific properties
            var customer = await _customerService.GetCustomerByEmailAsync(subscription.Email);
            if (customer != null && !await _customerService.IsGuestAsync(customer))
            {
                //try to add language
                var languageId = await _genericAttributeService.GetAttributeAsync<int>(customer, NopCustomerDefaults.LanguageIdAttribute);
                if (languageId > 0)
                    member.Language = (await _languageService.GetLanguageByIdAsync(languageId))?.UniqueSeoCode;

                //try to add names
                var firstName = await _genericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.FirstNameAttribute);
                var lastName = await _genericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.LastNameAttribute);
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
        private async Task<IList<Operation>> GetEcommerceApiOperationsAsync()
        {
            var operations = new List<Operation>();

            //prepare operations
            operations.AddRange(await GetStoreOperationsAsync());
            operations.AddRange(await GetCustomerOperationsAsync());
            operations.AddRange(await GetProductOperationsAsync());
            operations.AddRange(await GetProductVariantOperationsAsync());
            operations.AddRange(await GetOrderOperationsAsync());
            operations.AddRange(await GetCartOperationsAsync());

            return operations;
        }

        /// <summary>
        /// Get code of the primary store currency
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the CurrencyCode
        /// </returns>
        private async Task<CurrencyCode> GetCurrencyCodeAsync()
        {
            var currencyCode = (await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId))?.CurrencyCode;
            if (!Enum.TryParse(currencyCode, true, out CurrencyCode result))
                result = CurrencyCode.USD;

            return result;
        }

        #region Stores

        /// <summary>
        /// Get operations to manage stores
        /// </summary>
        /// <returns>The asynchronous task whose result contains the list of operations</returns>
        private async Task<IList<Operation>> GetStoreOperationsAsync()
        {
            //first create stores, we don't use batch operations, coz the store is the root object for all E-Commerce data 
            //and we need to make sure that it is created
            await CreateStoresAsync();

            var operations = new List<Operation>();

            //prepare operations
            operations.AddRange(await GetUpdateStoresOperationsAsync());
            operations.AddRange(GetDeleteStoresOperations());

            return operations;
        }

        /// <summary>
        /// Create stores
        /// </summary>
        /// <returns>The asynchronous task whose result determines whether stores successfully created</returns>
        private async Task<bool> CreateStoresAsync()
        {
            return await HandleRequestAsync(async () =>
            {
                //get created stores
                var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Store, OperationType.Create);
                var stores = await records.SelectAwait(async record => await _storeService.GetStoreByIdAsync(record.EntityId)).ToListAsync();

                foreach (var store in stores)
                {
                    var storeObject = await MapStoreAsync(store);
                    if (storeObject == null)
                        continue;

                    //create store
                    await HandleRequestAsync(async () => await _mailChimpManager.ECommerceStores.AddAsync(storeObject));
                }

                return true;
            });
        }

        /// <summary>
        /// Get operations to update stores
        /// </summary>
        /// <returns>The asynchronous task whose result contains the list of operations</returns>
        private async Task<IEnumerable<Operation>> GetUpdateStoresOperationsAsync()
        {
            var operations = new List<Operation>();

            //get updated stores
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Store, OperationType.Update);
            var stores = await records.SelectAwait(async record => await _storeService.GetStoreByIdAsync(record.EntityId)).ToListAsync();

            foreach (var store in stores)
            {
                var storeObject = await MapStoreAsync(store);
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
        /// <returns>The asynchronous task whose result contains the list of operations</returns>
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
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the Store
        /// </returns>
        private async Task<mailchimp.Store> MapStoreAsync(Store store)
        {
            var key = $"{nameof(MailChimpSettings)}.{nameof(MailChimpSettings.ListId)}";
            return store == null ? null : new mailchimp.Store
            {
                Id = string.Format(_mailChimpSettings.StoreIdMask, store.Id),
                ListId = await _settingService.GetSettingByKeyAsync<string>(key: key, storeId: store.Id, loadSharedValueIfNotFound: true),
                Name = store.Name,
                Domain = _webHelper.GetStoreLocation(),
                CurrencyCode = await GetCurrencyCodeAsync(),
                PrimaryLocale = (await _languageService.GetLanguageByIdAsync(store.DefaultLanguageId) ?? (await _languageService.GetAllLanguagesAsync()).FirstOrDefault())?.UniqueSeoCode,
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
        private async Task<IList<Operation>> GetCustomerOperationsAsync()
        {
            var operations = new List<Operation>();

            //prepare operations
            operations.AddRange(await GetCreateOrUpdateCustomersOperationsAsync());
            operations.AddRange(await GetDeleteCustomersOperationsAsync());

            return operations;
        }

        /// <summary>
        /// Get operations to create and update customers
        /// </summary>
        /// <returns>List of operations</returns>
        private async Task<IList<Operation>> GetCreateOrUpdateCustomersOperationsAsync()
        {
            var operations = new List<Operation>();

            //get created and updated customers
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Customer, OperationType.Create).ToList();
            records.AddRange(_synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Customer, OperationType.Update));
            var customers = await _customerService.GetCustomersByIdsAsync(records.Select(record => record.EntityId).Distinct().ToArray());

            foreach (var store in await _storeService.GetAllStoresAsync())
            {
                //create customers for all stores
                foreach (var customer in customers)
                {
                    var customerObject = await MapCustomerAsync(customer, store.Id);
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
        private async Task<IList<Operation>> GetDeleteCustomersOperationsAsync()
        {
            var operations = new List<Operation>();

            //get records of deleted customers
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Customer, OperationType.Delete);

            //add operations
            operations.AddRange((await _storeService.GetAllStoresAsync()).SelectMany(store => records.Select(record =>
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
        private async Task<mailchimp.Customer> MapCustomerAsync(Customer customer, int storeId)
        {
            if (customer == null)
                return null;

            //get all customer orders
            var customerOrders = (await _orderService.SearchOrdersAsync(storeId: storeId, customerId: customer.Id)).ToList();

            //get customer country and region
            var customerCountry = await _countryService.GetCountryByIdAsync(await _genericAttributeService.GetAttributeAsync<int>(customer, NopCustomerDefaults.CountryIdAttribute));
            var customerProvince = await _stateProvinceService.GetStateProvinceByIdAsync(await _genericAttributeService.GetAttributeAsync<int>(customer, NopCustomerDefaults.StateProvinceIdAttribute));

            return new mailchimp.Customer
            {
                Id = customer.Id.ToString(),
                EmailAddress = customer.Email,
                OptInStatus = false,
                OrdersCount = customerOrders.Count,
                TotalSpent = customerOrders.Sum(order => order.OrderTotal),
                FirstName = await _genericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.FirstNameAttribute),
                LastName = await _genericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.LastNameAttribute),
                Company = await _genericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.CompanyAttribute),
                Address = new mailchimp.Address
                {
                    Address1 = await _genericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.StreetAddressAttribute),
                    Address2 = await _genericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.StreetAddress2Attribute),
                    City = await _genericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.CityAttribute),
                    Province = customerProvince?.Name,
                    ProvinceCode = customerProvince?.Abbreviation,
                    Country = customerCountry?.Name,
                    CountryCode = customerCountry?.TwoLetterIsoCode,
                    PostalCode = await _genericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.ZipPostalCodeAttribute)
                }
            };
        }

        #endregion

        #region Products

        /// <summary>
        /// Get operations to manage products
        /// </summary>
        /// <returns>List of operations</returns>
        private async Task<IList<Operation>> GetProductOperationsAsync()
        {
            var operations = new List<Operation>();

            //prepare operations
            operations.AddRange(await GetCreateProductsOperationsAsync());
            operations.AddRange(await GetUpdateProductsOperationsAsync());
            operations.AddRange(await GetDeleteProductsOperationsAsync());

            return operations;
        }

        /// <summary>
        /// Get operations to create products
        /// </summary>
        /// <returns>List of operations</returns>
        private async Task<IList<Operation>> GetCreateProductsOperationsAsync()
        {
            var operations = new List<Operation>();

            //get created products
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Product, OperationType.Create);
            var products = await _productService.GetProductsByIdsAsync(records.Select(record => record.EntityId).ToArray());

            foreach (var store in await _storeService.GetAllStoresAsync())
            {
                //filter products by the store
                var storeProducts = await products.WhereAwait(async product => await _storeMappingService.AuthorizeAsync(product, store.Id)).ToListAsync();

                foreach (var product in storeProducts)
                {
                    var productObject = await MapProductAsync(product);
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
        private async Task<IList<Operation>> GetUpdateProductsOperationsAsync()
        {
            var operations = new List<Operation>();

            //get updated products
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Product, OperationType.Update);
            var products = await _productService.GetProductsByIdsAsync(records.Select(record => record.EntityId).ToArray());

            foreach (var store in await _storeService.GetAllStoresAsync())
            {
                //filter products by the store
                var storeProducts = await products.WhereAwait(async product => await _storeMappingService.AuthorizeAsync(product, store.Id)).ToListAsync();

                foreach (var product in storeProducts)
                {
                    var productObject = await MapProductAsync(product);
                    if (productObject == null)
                        continue;

                    //prepare request path and operation ID
                    var storeId = string.Format(_mailChimpSettings.StoreIdMask, store.Id);
                    var requestPath = string.Format(MailChimpDefaults.ProductsApiPath, storeId, product.Id);
                    var operationId = $"update-product-{product.Id}-store-{store.Id}";

                    //add operation
                    operations.Add(CreateOperation(productObject, OperationType.Update, requestPath, operationId));

                    //add operation to update default product variant
                    var productVariant = await CreateDefaultProductVariantByProductAsync(product);
                    if (productVariant == null)
                        continue;

                    var requestPathVariant = string.Format(MailChimpDefaults.ProductVariantsApiPath, storeId, product.Id, Guid.Empty.ToString());
                    var operationIdVariant = $"update-productVariant-{Guid.Empty}-product-{product.Id}-store-{store.Id}";
                    operations.Add(CreateOperation(productVariant, OperationType.Update, requestPathVariant, operationIdVariant));
                }
            }

            return operations;
        }

        /// <summary>
        /// Get operations to delete products
        /// </summary>
        /// <returns>List of operations</returns>
        private async Task<IList<Operation>> GetDeleteProductsOperationsAsync()
        {
            var operations = new List<Operation>();

            //get records of deleted products
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Product, OperationType.Delete);

            //add operations
            operations.AddRange((await _storeService.GetAllStoresAsync()).SelectMany(store => records.Select(record =>
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
        private async Task<mailchimp.Product> MapProductAsync(Product product)
        {
            return product == null ? null : new mailchimp.Product
            {
                Id = product.Id.ToString(),
                Title = product.Name,
                Url = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext)
                    .RouteUrl(nameof(Product), new { SeName = await _urlRecordService.GetSeNameAsync(product) }, _actionContextAccessor.ActionContext.HttpContext.Request.Scheme),
                Description = HtmlHelper.StripTags(!string.IsNullOrEmpty(product.FullDescription) ? product.FullDescription :
                    !string.IsNullOrEmpty(product.ShortDescription) ? product.ShortDescription : product.Name),
                Type = (await _categoryService.GetCategoryByIdAsync((await _categoryService.GetProductCategoriesByProductIdAsync(product.Id)).FirstOrDefault()?.CategoryId ?? 0))?.Name,
                Vendor = (await _manufacturerService.GetManufacturerByIdAsync((await _manufacturerService.GetProductManufacturersByProductIdAsync(product.Id))?.FirstOrDefault()?.ManufacturerId ?? 0))?.Name,
                ImageUrl = await _pictureService.GetPictureUrlAsync((await _pictureService.GetProductPictureAsync(product, null))?.Id ?? 0),
                Variants = await CreateProductVariantsByProductAsync(product)
            };
        }

        /// <summary>
        /// Create MailChimp product variant objects by nopCommerce product object
        /// </summary>
        /// <param name="product">Product</param>
        /// <returns>List of product variants</returns>
        private async Task<IList<mailchimp.Variant>> CreateProductVariantsByProductAsync(Product product)
        {
            var variants = new List<mailchimp.Variant>
            {
                //add default variant
                await CreateDefaultProductVariantByProductAsync(product)
            };

            //add variants from attribute combinations
            var combinationVariants = await (await _productAttributeService.GetAllProductAttributeCombinationsAsync(product.Id))
                .Where(combination => combination?.ProductId > 0)
                .SelectAwait(async combination => await CreateProductVariantByAttributeCombinationAsync(combination)).ToListAsync();
            variants.AddRange(combinationVariants);

            return variants;
        }

        /// <summary>
        /// Create MailChimp product variant object by nopCommerce product object
        /// </summary>
        /// <param name="product">Product</param>
        /// <returns>Product variant</returns>
        private async Task<mailchimp.Variant> CreateDefaultProductVariantByProductAsync(Product product)
        {
            return product == null ? null : new mailchimp.Variant
            {
                Id = Guid.Empty.ToString(), //set empty guid as identifier for default product variant
                Title = product.Name,
                Url = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext)
                    .RouteUrl(nameof(Product), new { SeName = await _urlRecordService.GetSeNameAsync(product) }, _actionContextAccessor.ActionContext.HttpContext.Request.Scheme),
                Sku = product.Sku,
                Price = product.Price,
                ImageUrl = await _pictureService.GetPictureUrlAsync((await _pictureService.GetProductPictureAsync(product, null))?.Id ?? 0),
                InventoryQuantity = product.ManageInventoryMethod != ManageInventoryMethod.DontManageStock ? product.StockQuantity : int.MaxValue,
                Visibility = product.Published.ToString().ToLower()
            };
        }

        /// <summary>
        /// Create MailChimp product variant object by nopCommerce product attribute combination object
        /// </summary>
        /// <param name="combination">Product attribute combination</param>
        /// <returns>Product variant</returns>
        private async Task<mailchimp.Variant> CreateProductVariantByAttributeCombinationAsync(ProductAttributeCombination combination)
        {
            if (combination?.ProductId == null || combination?.ProductId == 0)
                return null;

            var product = await _productService.GetProductByIdAsync(combination.ProductId);

            return new mailchimp.Variant
            {
                Id = combination.Id.ToString(),
                Title = product.Name,
                Url = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).RouteUrl(nameof(Product), 
                new { SeName = await _urlRecordService.GetSeNameAsync(product) }, 
                    _actionContextAccessor.ActionContext.HttpContext.Request.Scheme),
                Sku = !string.IsNullOrEmpty(combination.Sku) ? combination.Sku : product.Sku,
                Price = combination.OverriddenPrice ?? product.Price,
                InventoryQuantity = product.ManageInventoryMethod == ManageInventoryMethod.ManageStockByAttributes
                    ? combination.StockQuantity : product.ManageInventoryMethod != ManageInventoryMethod.DontManageStock
                    ? product.StockQuantity : int.MaxValue,
                ImageUrl = await _pictureService.GetPictureUrlAsync((await _pictureService.GetProductPictureAsync(product, combination.AttributesXml))?.Id ?? 0),
                Visibility = product.Published.ToString().ToLowerInvariant()
            };
        }

        /// <summary>
        /// Get operations to manage product variants
        /// </summary>
        /// <returns>List of operations</returns>
        private async Task<IList<Operation>> GetProductVariantOperationsAsync()
        {
            var operations = new List<Operation>();

            //prepare operations
            operations.AddRange(await GetCreateOrUpdateProductVariantsOperationsAsync());
            operations.AddRange(await GetDeleteProductVariantsOperationsAsync());

            return operations;
        }

        /// <summary>
        /// Get operations to create and update product variants
        /// </summary>
        /// <returns>List of operations</returns>
        private async Task<IList<Operation>> GetCreateOrUpdateProductVariantsOperationsAsync()
        {
            var operations = new List<Operation>();

            //get created and updated product combinations
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.AttributeCombination, OperationType.Create).ToList();
            records.AddRange(_synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.AttributeCombination, OperationType.Update));
            var combinations = await records.Distinct().SelectAwait(async record => await _productAttributeService.GetProductAttributeCombinationByIdAsync(record.EntityId)).ToListAsync();

            foreach (var store in await _storeService.GetAllStoresAsync())
            {
                //filter combinations by the store
                var storeCombinations = await combinations.WhereAwait(async combination => 
                    await _storeMappingService.AuthorizeAsync(await _productService.GetProductByIdAsync(combination.ProductId), store.Id)).ToListAsync();

                foreach (var combination in storeCombinations)
                {
                    var productVariant = await CreateProductVariantByAttributeCombinationAsync(combination);
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
        private async Task<IList<Operation>> GetDeleteProductVariantsOperationsAsync()
        {
            var operations = new List<Operation>();

            //get records of deleted product combinations
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.AttributeCombination, OperationType.Delete);

            //add operations
            operations.AddRange((await _storeService.GetAllStoresAsync()).SelectMany(store => records.Select(record =>
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
        private async Task<IList<Operation>> GetOrderOperationsAsync()
        {
            var operations = new List<Operation>();

            //prepare operations
            operations.AddRange(await GetCreateOrdersOperationsAsync());
            operations.AddRange(await GetUpdateOrdersOperationsAsync());
            operations.AddRange(await GetDeleteOrdersOperationsAsync());

            return operations;
        }

        /// <summary>
        /// Get operations to create orders
        /// </summary>
        /// <returns>List of operations</returns>
        private async Task<IList<Operation>> GetCreateOrdersOperationsAsync()
        {
            var operations = new List<Operation>();

            //get created orders
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Order, OperationType.Create);
            var orders = await (await _orderService.GetOrdersByIdsAsync(records.Select(record => record.EntityId).ToArray()))
                .WhereAwait(async order => !await _customerService.IsGuestAsync(await _customerService.GetCustomerByIdAsync(order.CustomerId))).ToListAsync();

            foreach (var store in await _storeService.GetAllStoresAsync())
            {
                //filter orders by the store
                var storeOrders = orders.Where(order => order?.StoreId == store.Id);

                foreach (var order in storeOrders)
                {
                    var orderObject = await MapOrderAsync(order);
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
        private async Task<IList<Operation>> GetUpdateOrdersOperationsAsync()
        {
            var operations = new List<Operation>();

            //get updated orders
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Order, OperationType.Update);
            var orders = await (await _orderService.GetOrdersByIdsAsync(records.Select(record => record.EntityId).ToArray()))
                .WhereAwait(async order => !await _customerService.IsGuestAsync(await _customerService.GetCustomerByIdAsync(order.CustomerId))).ToListAsync();

            foreach (var store in await _storeService.GetAllStoresAsync())
            {
                //filter orders by the store
                var storeOrders = orders.Where(order => order?.StoreId == store.Id);

                foreach (var order in storeOrders)
                {
                    var orderObject = await MapOrderAsync(order);
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
        private async Task<IList<Operation>> GetDeleteOrdersOperationsAsync()
        {
            var operations = new List<Operation>();

            //get records of deleted orders
            var records = _synchronizationRecordService.GetRecordsByEntityTypeAndOperationType(EntityType.Order, OperationType.Delete);

            //add operations
            operations.AddRange((await _storeService.GetAllStoresAsync()).SelectMany(store => records.Select(record =>
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
        private async Task<mailchimp.Order> MapOrderAsync(Order order)
        {
            return order == null ? null : new mailchimp.Order
            {
                Id = order.Id.ToString(),
                Customer = new mailchimp.Customer { Id = order.CustomerId.ToString() },
                FinancialStatus = order.PaymentStatus.ToString("D"),
                FulfillmentStatus = order.OrderStatus.ToString("D"),
                CurrencyCode = await GetCurrencyCodeAsync(),
                OrderTotal = order.OrderTotal,
                TaxTotal = order.OrderTax,
                ShippingTotal = order.OrderShippingInclTax,
                ProcessedAtForeign = order.CreatedOnUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ShippingAddress = order.PickupInStore && order.PickupAddressId != null ? 
                    await MapOrderAddressAsync(await _addressService.GetAddressByIdAsync(order.PickupAddressId ?? 0)) :
                    await MapOrderAddressAsync(await _addressService.GetAddressByIdAsync(order.ShippingAddressId ?? 0)),
                BillingAddress = await MapOrderAddressAsync(await _addressService.GetAddressByIdAsync(order.BillingAddressId)),
                Lines = await (await _orderService.GetOrderItemsAsync(order.Id)).SelectAwait(async item => await MapOrderItemAsync(item)).ToListAsync()
            };
        }

        /// <summary>
        /// Create MailChimp order address object by nopCommerce address object
        /// </summary>
        /// <param name="address">Address</param>
        /// <returns>Order address</returns>
        private async Task<mailchimp.OrderAddress> MapOrderAddressAsync(Address address)
        {
            if (address == null)
                return null;

            var stateProvince = await _stateProvinceService.GetStateProvinceByAddressAsync(address);
            var country = await _countryService.GetCountryByAddressAsync(address);

            return new mailchimp.OrderAddress
            {
                Phone = address.PhoneNumber,
                Company = address.Company,
                Address1 = address.Address1,
                Address2 = address.Address2,
                City = address.City,
                Province = stateProvince?.Name,
                ProvinceCode = stateProvince?.Abbreviation,
                Country = country?.Name,
                CountryCode = country?.TwoLetterIsoCode,
                PostalCode = address.ZipPostalCode
            };
        }

        /// <summary>
        /// Create MailChimp line object by nopCommerce order item object
        /// </summary>
        /// <param name="item">Order item</param>
        /// <returns>Line</returns>
        private async Task<mailchimp.Line> MapOrderItemAsync(OrderItem item)
        {
            var product = await _productService.GetProductByIdAsync(item?.ProductId ?? 0);
            return product == null ? null : new mailchimp.Line
            {
                Id = item.Id.ToString(),
                ProductId = item.ProductId.ToString(),
                ProductVariantId = (await _productAttributeParser
                    .FindProductAttributeCombinationAsync(product, item.AttributesXml))?.Id.ToString() ?? Guid.Empty.ToString(),
                Price = item.PriceInclTax,
                Quantity = item.Quantity
            };
        }

        #endregion

        #region Carts

        /// <summary>
        /// Get operations to manage carts
        /// </summary>
        /// <returns>The asynchronous task whose result contains the list of operations</returns>
        private async Task<IList<Operation>> GetCartOperationsAsync()
        {
            var operations = new List<Operation>();

            //get customers with shopping cart
            var customersWithCart = await (await _customerService.GetCustomersWithShoppingCartsAsync(ShoppingCartType.ShoppingCart))
                .WhereAwait(async customer => !await _customerService.IsGuestAsync(await _customerService.GetCustomerByIdAsync(customer.Id))).ToListAsync();

            foreach (var store in await _storeService.GetAllStoresAsync())
            {
                var storeId = string.Format(_mailChimpSettings.StoreIdMask, store.Id);

                //filter customers with cart by the store
                var storeCustomersWithCart = await customersWithCart
                    .WhereAwait(async customer => (await _shoppingCartService.GetShoppingCartAsync(customer)).Any(cart => cart?.StoreId == store.Id)).ToListAsync();

                //get existing carts on MailChimp
                var cartsIds = await HandleRequestAsync(async () =>
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
                    var cart = await CreateCartByCustomerAsync(customer, store.Id);
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
                    var cart = await CreateCartByCustomerAsync(customer, store.Id);
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
        private async Task<mailchimp.Cart> CreateCartByCustomerAsync(Customer customer, int storeId)
        {
            if (customer == null)
                return null;

            //create cart lines
            var lines = await (await _shoppingCartService.GetShoppingCartAsync(customer))
                .Where(cart => cart?.StoreId == storeId)
                .SelectAwait(async item => await MapShoppingCartItemAsync(item))
                .Where(line => line != null).ToListAsync();

            return new mailchimp.Cart
            {
                Id = customer.Id.ToString(),
                Customer = new mailchimp.Customer { Id = customer.Id.ToString() },
                CheckoutUrl = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext)
                    .RouteUrl("ShoppingCart", null, _actionContextAccessor.ActionContext.HttpContext.Request.Scheme),
                CurrencyCode = await GetCurrencyCodeAsync(),
                OrderTotal = lines.Sum(line => line.Price),
                Lines = lines
            };
        }

        /// <summary>
        /// Create MailChimp line object by nopCommerce shopping cart item object
        /// </summary>
        /// <param name="item">Shopping cart item</param>
        /// <returns>Line</returns>
        private async Task<mailchimp.Line> MapShoppingCartItemAsync(ShoppingCartItem item)
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);
            var (subTotal, _, _, _) = await _shoppingCartService.GetSubTotalAsync(item, true);
            return item?.ProductId == null ? null : new mailchimp.Line
            {
                Id = item.Id.ToString(),
                ProductId = item.ProductId.ToString(),
                ProductVariantId = (await _productAttributeParser.FindProductAttributeCombinationAsync(product, item.AttributesXml))?.Id.ToString() ?? Guid.Empty.ToString(),
                Price = subTotal,
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
        public async Task<int?> SynchronizeAsync(bool manualSynchronization = false)
        {
            return await HandleRequestAsync<int?>(async () =>
            {
                //prepare records to manual synchronization
                if (manualSynchronization)
                {
                    var recordsPrepared = await PrepareRecordsToManualSynchronizationAsync();
                    if (!recordsPrepared)
                        return null;
                }

                //prepare batch webhook
                var webhookPrepared = await PrepareBatchWebhookAsync();
                if (!webhookPrepared)
                    return null;

                var operations = new List<Operation>();

                //preare subscription operations
                operations.AddRange(await GetSubscriptionsOperationsAsync());

                //prepare E-Commerce operations
                if (_mailChimpSettings.PassEcommerceData)
                    operations.AddRange(await GetEcommerceApiOperationsAsync());

                //start synchronization
                var batchNumber = operations.Count / _mailChimpSettings.BatchOperationNumber +
                    (operations.Count % _mailChimpSettings.BatchOperationNumber > 0 ? 1 : 0);
                for (var i = 0; i < batchNumber; i++)
                {
                    var batchOperations = operations.Skip(i * _mailChimpSettings.BatchOperationNumber).Take(_mailChimpSettings.BatchOperationNumber);
                    var batch = await _mailChimpManager.Batches.AddAsync(new BatchRequest { Operations = batchOperations })
                        ?? throw new NopException("No response from the service");
                }

                //synchronization successfully started, thus delete records
                if (_mailChimpSettings.PassEcommerceData)
                    await _synchronizationRecordService.ClearRecordsAsync();
                else
                    await _synchronizationRecordService.DeleteRecordsByEntityTypeAsync(EntityType.Subscription);

                return operations.Count;
            });
        }

        /// <summary>
        /// Get account information
        /// </summary>
        /// <returns>The asynchronous task whose result contains the account information</returns>
        public async Task<string> GetAccountInfoAsync()
        {
            return await HandleRequestAsync(async () =>
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
        public async Task<IList<SelectListItem>> GetAvailableListsAsync()
        {
            return await HandleRequestAsync(async () =>
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
        public async Task<bool> PrepareWebhookAsync(string listId)
        {
            return await HandleRequestAsync(async () =>
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
        public async Task<bool> DeleteWebhooksAsync()
        {
            return await HandleRequestAsync(async () =>
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
                    await HandleRequestAsync(async () =>
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
        public async Task<bool> DeleteBatchWebhookAsync()
        {
            return await HandleRequestAsync(async () =>
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
        public async Task<(string Id, int? CompletedOperationNumber)> HandleBatchWebhookAsync(IFormCollection form, IDictionary<string, int> handledBatchesInfo)
        {
            return await HandleRequestAsync<(string, int?)>(async () =>
            {
                var batchWebhookType = "batch_operation_completed";
                if (!form.TryGetValue("type", out var webhookType) || !webhookType.Equals(batchWebhookType))
                    return (null, null);

                var completeStatus = "finished";
                if (!form.TryGetValue("data[status]", out var batchStatus) || !batchStatus.Equals(completeStatus))
                    return (null, null);

                if (!form.TryGetValue("data[id]", out var batchId))
                    return (null, null);

                //ensure that this batch is not yet handled
                var alreadyHandledBatchInfo = handledBatchesInfo.FirstOrDefault(batchInfo => batchInfo.Key.Equals(batchId));
                if (!alreadyHandledBatchInfo.Equals(default(KeyValuePair<string, int>)))
                    return (alreadyHandledBatchInfo.Key, alreadyHandledBatchInfo.Value);

                //log and return results
                var completedOperationNumber = await LogSynchronizationResultAsync(batchId);

                return (batchId, completedOperationNumber);
            });
        }

        /// <summary>
        /// Handle webhook
        /// </summary>
        /// <param name="form">Request form parameters</param>
        /// <returns>The asynchronous task whose result determines whether the webhook successfully handled</returns>
        public async Task<bool> HandleWebhookAsync(IFormCollection form)
        {
            return await HandleRequestAsync(async () =>
            {
                //try to get subscriber list identifier
                if (!form.TryGetValue("data[list_id]", out var listId))
                    return false;

                //get stores that tied to a specific MailChimp list
                var settingsName = $"{nameof(MailChimpSettings)}.{nameof(MailChimpSettings.ListId)}";
                var storeIds = await (await _storeService.GetAllStoresAsync())
                    .WhereAwait(async store => listId.Equals(await _settingService.GetSettingByKeyAsync<string>(settingsName, storeId: store.Id, loadSharedValueIfNotFound: true)))
                    .Select(store => store.Id).ToListAsync();

                if (!form.TryGetValue("data[email]", out var email))
                    return false;

                if (!form.TryGetValue("type", out var webhookType))
                    return false;

                //deactivate subscriptions
                var unsubscribeType = "unsubscribe";
                var cleanedType = "cleaned";
                if (webhookType.Equals(unsubscribeType) || webhookType.Equals(cleanedType))
                {
                    //get existing subscriptions by email
                    var subscriptions = await storeIds
                        .SelectAwait(async storeId => await _newsLetterSubscriptionService.GetNewsLetterSubscriptionByEmailAndStoreIdAsync(email, storeId))
                        .Where(subscription => !string.IsNullOrEmpty(subscription?.Email)).ToListAsync();

                    foreach (var subscription in subscriptions)
                    {
                        //deactivate
                        subscription.Active = false;
                        await _newsLetterSubscriptionService.UpdateNewsLetterSubscriptionAsync(subscription, false);
                        await _logger.InformationAsync($"MailChimp info. Email {subscription.Email} was unsubscribed from the store #{subscription.StoreId}");
                    }
                }

                //activate subscriptions
                var subscribeType = "subscribe";
                if (webhookType.Equals(subscribeType))
                {
                    foreach (var storeId in storeIds)
                    {
                        var subscription = await _newsLetterSubscriptionService.GetNewsLetterSubscriptionByEmailAndStoreIdAsync(email, storeId);

                        //if subscription doesn't exist, create the new one
                        if (subscription == null)
                        {
                            await _newsLetterSubscriptionService.InsertNewsLetterSubscriptionAsync(new NewsLetterSubscription
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
                            await _newsLetterSubscriptionService.UpdateNewsLetterSubscriptionAsync(subscription, false);

                        }
                        await _logger.InformationAsync($"MailChimp info. Email {subscription.Email} has been subscribed to the store #{subscription.StoreId}");
                    }
                }

                return await Task.FromResult(true);
            });
        }

        #endregion
    }
}