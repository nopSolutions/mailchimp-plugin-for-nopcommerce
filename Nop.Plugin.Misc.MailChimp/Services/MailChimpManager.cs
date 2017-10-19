using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using mailChimp = MailChimp.Net;
using MailChimp.Net.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using model = MailChimp.Net.Models;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Plugin.Misc.MailChimp.Domain;
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
using Nop.Services.Catalog;
using Nop.Services.Seo;
using Nop.Services.Stores;

namespace Nop.Plugin.Misc.MailChimp.Services
{
    /// <summary>
    /// Represents MailChimp manager
    /// </summary>
    public class MailChimpManager
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
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
        private readonly IStoreContext _storeContext;
        private readonly IStoreMappingService _storeMappingService;
        private readonly IStoreService _storeService;
        private readonly ISynchronizationRecordService _synchronizationRecordService;
        private readonly IWebHelper _webHelper;

        #endregion

        #region Ctor

        public MailChimpManager(CurrencySettings currencySettings,
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
            IStoreContext storeContext,
            IStoreMappingService storeMappingService,
            IStoreService storeService,
            ISynchronizationRecordService synchronizationRecordService,
            IWebHelper webHelper)
        {
            this._currencySettings = currencySettings;
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
            this._storeContext = storeContext;
            this._storeService = storeService;
            this._synchronizationRecordService = synchronizationRecordService;
            this._webHelper = webHelper;
        }

        #endregion

        #region Properties

        private mailChimp.MailChimpManager _manager;
        /// <summary>
        /// Get single MailChimp manager for the requesting service
        /// </summary>
        private mailChimp.MailChimpManager Manager
        {
            get
            {
                if (_manager == null)
                    _manager = new mailChimp.MailChimpManager(_settingService.LoadSetting<MailChimpSettings>().ApiKey);


                return _manager;
            }
        }

        /// <summary>
        /// Check that manager is configured
        /// </summary>
        public bool IsConfigured
        {
            get { return !string.IsNullOrEmpty(_settingService.LoadSetting<MailChimpSettings>().ApiKey); }
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Check that batch operation is completed
        /// </summary>
        /// <param name="batch">Batch</param>
        /// <returns>True if batch status is finished; otherwise false</returns>
        protected bool BatchOperationIsComplete(Batch batch)
        {
            return batch.Status.Equals("finished", StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Log synchronization information after batch is complete
        /// </summary>
        /// <param name="batch">Batch</param>
        protected async void LogAfterComplete(Batch batch)
        {
            do
            {
                //check batch status each 10 seconds
                await Task.Delay(10000);
                batch = await Manager.Batches.GetBatchStatus(batch.Id);
            } while (!BatchOperationIsComplete(batch));

            //log
            try
            {
                _logger.Information(string.Format(@"MailChimp synchronization: Started at: {0}, Finished operations: {1}, Errored operations: {2},
                    Total operations: {3}, Completed at: {4}, Batch ID: {5}", batch.SubmittedAt, batch.FinishedOperations, batch.ErroredOperations,
                    batch.TotalOperations, batch.CompletedAt, batch.Id));
            }
            catch (ArgumentException) { }
            
        }

        /// <summary>
        /// Get code of the primary store currency
        /// </summary>
        /// <returns>Currency code</returns>
        protected CurrencyCode GetCurrencyCode()
        {
            var result = CurrencyCode.USD;
            var currency = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId);
            if (currency == null)
                return result;

            Enum.TryParse(currency.CurrencyCode, true, out result);

            return result;
        }

        #region Subscriptions

        /// <summary>
        /// Get batch of subscription operations
        /// </summary>
        /// <param name="mailChimpSettings">mMilChimp settings</param>
        /// <returns>List of operations</returns>
        protected IList<Operation> GetSubscriptionOperations(MailChimpSettings mailChimpSettings)
        {
            var subscriptionsOperations = new List<Operation>();

            subscriptionsOperations.AddRange(GetNewSubscriptions());
            subscriptionsOperations.AddRange(GetUpdatedSubscriptions());
            subscriptionsOperations.AddRange(GetDeletedSubscriptions());

            //delete records
            _synchronizationRecordService.DeleteRecordsByEntityType(EntityType.Subscription);

            return subscriptionsOperations;
        }

        /// <summary>
        /// Get batch of new subscription operations
        /// </summary>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetNewSubscriptions()
        {
            //get new subscriptions
            var newSubscriptions = _synchronizationRecordService.GetRecordsByEntityTypeAndActionType(EntityType.Subscription, ActionType.Create)
                .Select(record => _newsLetterSubscriptionService.GetNewsLetterSubscriptionById(record.EntityId));

            return _storeService.GetAllStores().SelectMany(store =>
            {
                var listId = _settingService.GetSettingByKey<string>("mailchimpsettings.listid", storeId: store.Id, loadSharedValueIfNotFound: true);
                if (string.IsNullOrEmpty(listId))
                    return new List<Operation>();

                return newSubscriptions.Where(subscription => subscription.StoreId == store.Id).Select(subscription =>
                {
                    //create new subscription
                    var newMember = new model.Member
                    {
                        EmailAddress = subscription.Email,
                        Status = subscription.Active ? model.Status.Subscribed : model.Status.Unsubscribed,
                        StatusIfNew = subscription.Active ? model.Status.Subscribed : model.Status.Unsubscribed,
                        TimestampSignup = subscription.CreatedOnUtc.ToString("s")
                    };

                    //if customer not quest add some properties
                    var customer = _customerService.GetCustomerByEmail(subscription.Email);
                    if (customer != null && !customer.IsGuest())
                    {
                        var language = _languageService.GetLanguageById(customer.GetAttribute<int>(SystemCustomerAttributeNames.LanguageId));
                        newMember.Language = language != null ? language.UniqueSeoCode : string.Empty;
                        newMember.MergeFields = new Dictionary<string, string>
                        {
                            { "FNAME", customer.GetAttribute<string>(SystemCustomerAttributeNames.FirstName) ?? string.Empty },
                            { "LNAME", customer.GetAttribute<string>(SystemCustomerAttributeNames.LastName) ?? string.Empty }
                        };
                    }

                    return new Operation
                    {
                        Method = "PUT",
                        Path = string.Format("/lists/{0}/members/{1}", listId, Manager.Members.Hash(subscription.Email)),
                        OperationId = string.Format("create_subscription_#{0}_to_list_{1}", subscription.Id, listId),
                        Body = JsonConvert.SerializeObject(newMember, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
                    };
                });
            });
        }

        /// <summary>
        /// Get batch of updated subscription operations
        /// </summary>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetUpdatedSubscriptions()
        {
            //get updated subscriptions
            var updatedSubscriptions = _synchronizationRecordService.GetRecordsByEntityTypeAndActionType(EntityType.Subscription, ActionType.Update)
                .Select(record => _newsLetterSubscriptionService.GetNewsLetterSubscriptionById(record.EntityId));

            return _storeService.GetAllStores().SelectMany(store =>
            {
                var listId = _settingService.GetSettingByKey<string>("mailchimpsettings.listid", storeId: store.Id, loadSharedValueIfNotFound: true);
                if (string.IsNullOrEmpty(listId))
                    return new List<Operation>();

                return updatedSubscriptions.Where(subscription => subscription.StoreId == store.Id).Select(subscription =>
                {
                    //create subscription
                    var newMember = new model.Member
                    {
                        EmailAddress = subscription.Email,
                        Status = subscription.Active ? model.Status.Subscribed : model.Status.Unsubscribed,
                        StatusIfNew = subscription.Active ? model.Status.Subscribed : model.Status.Unsubscribed,
                        TimestampSignup = subscription.CreatedOnUtc.ToString("s")
                    };

                    //if customer not quest add some properties
                    var customer = _customerService.GetCustomerByEmail(subscription.Email);
                    if (customer != null && !customer.IsGuest())
                    {
                        var language = _languageService.GetLanguageById(customer.GetAttribute<int>(SystemCustomerAttributeNames.LanguageId));
                        newMember.Language = language != null ? language.UniqueSeoCode : string.Empty;
                        newMember.MergeFields = new Dictionary<string, string>
                        {
                            { "FNAME", customer.GetAttribute<string>(SystemCustomerAttributeNames.FirstName) ?? string.Empty },
                            { "LNAME", customer.GetAttribute<string>(SystemCustomerAttributeNames.LastName) ?? string.Empty }
                        };
                    }

                    return new Operation
                    {
                        Method = "PUT",
                        Path = string.Format("/lists/{0}/members/{1}", listId, Manager.Members.Hash(subscription.Email)),
                        OperationId = string.Format("update_subscription_#{0}_on_list_{1}", subscription.Id, listId),
                        Body = JsonConvert.SerializeObject(newMember, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
                    };
                });
            });
        }

        /// <summary>
        /// Get batch of deleted subscription operations
        /// </summary>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetDeletedSubscriptions()
        {
            //get deleted records
            var deletedSubscriptions = _synchronizationRecordService.GetRecordsByEntityTypeAndActionType(EntityType.Subscription, ActionType.Delete);

            return _storeService.GetAllStores().SelectMany(store =>
            {
                var listId = _settingService.GetSettingByKey<string>("mailchimpsettings.listid", storeId: store.Id, loadSharedValueIfNotFound: true);
                if (string.IsNullOrEmpty(listId))
                    return new List<Operation>();

                //a little workaround here - we check if subscription already not exists for the particular store then remove it from MailChimp
                return deletedSubscriptions.Where(subscription =>
                    _newsLetterSubscriptionService.GetNewsLetterSubscriptionByEmailAndStoreId(subscription.Email, store.Id) == null)
                    .Select(subscription => new Operation
                    {
                        Method = "DELETE",
                        Path = string.Format("/lists/{0}/members/{1}", listId, Manager.Members.Hash(subscription.Email)),
                        OperationId = string.Format("delete_subscription_from_list_{0}", listId)
                    });
            });
        }

        #endregion

        #region Ecommerce API

        /// <summary>
        /// Get batch of operations for the MailChimp Ecommerce API
        /// </summary>
        /// <param name="mailChimpSettings">MailChimp settings</param>
        /// <returns>List of operations</returns>
        protected async Task<IList<Operation>> GetEcommerceApiOperations(MailChimpSettings mailChimpSettings)
        {
            var ecommerceApiOperations = new List<Operation>();

            ecommerceApiOperations.AddRange(GetStoreOperations());
            ecommerceApiOperations.AddRange(GetCustomerOperations());
            ecommerceApiOperations.AddRange(GetProductOperations());
            ecommerceApiOperations.AddRange(GetProductVariantOperations());
            ecommerceApiOperations.AddRange(GetOrderOperations());
            ecommerceApiOperations.AddRange(await GetCartOperations());

            return ecommerceApiOperations;
        }

        #region Stores

        /// <summary>
        /// Get batch of store operations
        /// </summary>
        /// <returns>List of operations</returns>
        protected IList<Operation> GetStoreOperations()
        {
            var storeOperations = new List<Operation>();

            storeOperations.AddRange(GetNewStores());
            storeOperations.AddRange(GetUpdatedStores());
            storeOperations.AddRange(GetDeletedStores());

            //delete records
            _synchronizationRecordService.DeleteRecordsByEntityType(EntityType.Store);

            return storeOperations;
        }

        /// <summary>
        /// Get batch of new store operations
        /// </summary>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetNewStores()
        {
            var currency = GetCurrencyCode();

            return _synchronizationRecordService.GetRecordsByEntityTypeAndActionType(EntityType.Store, ActionType.Create)
                .Select(record => _storeService.GetStoreById(record.EntityId)).Select(store => new Operation
                {
                    Method = "POST",
                    Path = "/ecommerce/stores",
                    OperationId = string.Format("create_store_{0}", store.Name),
                    Body = JsonConvert.SerializeObject(new model.Store
                    {
                        Id = store.Id.ToString(),
                        ListId = _settingService.GetSettingByKey<string>("mailchimpsettings.listid", storeId: store.Id, loadSharedValueIfNotFound: true),
                        Name = store.Name,
                        Domain = _webHelper.GetStoreLocation(store.SslEnabled),
                        CurrencyCode = currency,
                        PrimaryLocale = (_languageService.GetLanguageById(store.DefaultLanguageId) ?? _languageService.GetAllLanguages().First()).UniqueSeoCode,
                        Phone = store.CompanyPhoneNumber,
                        Timezone = _dateTimeHelper.DefaultStoreTimeZone != null ? _dateTimeHelper.DefaultStoreTimeZone.StandardName : string.Empty
                    }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
                });
        }

        /// <summary>
        /// Get batch of updated store operations
        /// </summary>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetUpdatedStores()
        {
            var currency = GetCurrencyCode();

            return _synchronizationRecordService.GetRecordsByEntityTypeAndActionType(EntityType.Store, ActionType.Update)
                .Select(record => _storeService.GetStoreById(record.EntityId)).Select(store => new Operation
                {
                    Method = "PATCH",
                    Path = string.Format("/ecommerce/stores/{0}", store.Id),
                    OperationId = string.Format("update_store_{0}", store.Name),
                    Body = JsonConvert.SerializeObject(new model.Store
                    {
                        ListId = _settingService.GetSettingByKey<string>("mailchimpsettings.listid", storeId: store.Id, loadSharedValueIfNotFound: true),
                        Name = store.Name,
                        Domain = _webHelper.GetStoreLocation(store.SslEnabled),
                        CurrencyCode = currency,
                        PrimaryLocale = (_languageService.GetLanguageById(store.DefaultLanguageId) ?? _languageService.GetAllLanguages().First()).UniqueSeoCode,
                        Phone = store.CompanyPhoneNumber,
                        Timezone = _dateTimeHelper.DefaultStoreTimeZone != null ? _dateTimeHelper.DefaultStoreTimeZone.StandardName : string.Empty
                    }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
                });
        }

        /// <summary>
        /// Get batch of deleted store operations
        /// </summary>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetDeletedStores()
        {
            return _synchronizationRecordService.GetRecordsByEntityTypeAndActionType(EntityType.Store, ActionType.Delete)
                .Select(record => new Operation
                {
                    Method = "DELETE",
                    Path = string.Format("/ecommerce/stores/{0}", record.EntityId),
                    OperationId = string.Format("delete_store_#{0}", record.EntityId)
                });
        }

        #endregion

        #region Customers

        /// <summary>
        /// Get batch of customer operations
        /// </summary>
        /// <returns>List of operations</returns>
        protected IList<Operation> GetCustomerOperations()
        {
            var customerOperations = new List<Operation>();

            customerOperations.AddRange(GetNewCustomers());
            customerOperations.AddRange(GetUpdatedCustomers());
            customerOperations.AddRange(GetDeletedCustomers());

            //delete records
            _synchronizationRecordService.DeleteRecordsByEntityType(EntityType.Customer);

            return customerOperations;
        }

        /// <summary>
        /// Get batch of new customer operations
        /// </summary>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetNewCustomers()
        {
            //get new customers
            var newCustomers = _customerService.GetCustomersByIds(_synchronizationRecordService
                .GetRecordsByEntityTypeAndActionType(EntityType.Customer, ActionType.Create).Select(record => record.EntityId).ToArray());

            return _storeService.GetAllStores().SelectMany(store => newCustomers.Select(customer =>
            {
                var customerOrders = _orderService.SearchOrders(storeId: store.Id, customerId: customer.Id);
                var customerCountry = _countryService.GetCountryById(customer.GetAttribute<int>(SystemCustomerAttributeNames.CountryId));
                var customerProvince = _stateProvinceService.GetStateProvinceById(customer.GetAttribute<int>(SystemCustomerAttributeNames.StateProvinceId));

                return new Operation
                {
                    Method = "PUT",
                    Path = string.Format("/ecommerce/stores/{0}/customers/{1}", store.Id, customer.Id),
                    OperationId = string.Format("create_customer_#{0}_to_store_{1}", customer.Id, store.Name),
                    Body = JsonConvert.SerializeObject(new model.Customer
                    {
                        Id = customer.Id.ToString(),
                        EmailAddress = customer.Email,
                        OptInStatus = false,
                        OrdersCount = customerOrders.TotalCount,
                        TotalSpent = (double)customerOrders.Sum(order => order.OrderTotal),
                        FirstName = customer.GetAttribute<string>(SystemCustomerAttributeNames.FirstName),
                        LastName = customer.GetAttribute<string>(SystemCustomerAttributeNames.LastName),
                        Company = customer.GetAttribute<string>(SystemCustomerAttributeNames.Company),
                        Address = new model.Address
                        {
                            Address1 = customer.GetAttribute<string>(SystemCustomerAttributeNames.StreetAddress) ?? string.Empty,
                            Address2 = customer.GetAttribute<string>(SystemCustomerAttributeNames.StreetAddress2) ?? string.Empty,
                            City = customer.GetAttribute<string>(SystemCustomerAttributeNames.City) ?? string.Empty,
                            Province = customerProvince != null ? customerProvince.Name : string.Empty,
                            ProvinceCode = customerProvince != null ? customerProvince.Abbreviation : string.Empty,
                            Country = customerCountry != null ? customerCountry.Name : string.Empty,
                            CountryCode = customerCountry != null ? customerCountry.TwoLetterIsoCode : string.Empty,
                            PostalCode = customer.GetAttribute<string>(SystemCustomerAttributeNames.ZipPostalCode) ?? string.Empty
                        }
                    }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
                };
            }));
        }

        /// <summary>
        /// Get batch of updated customer operations
        /// </summary>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetUpdatedCustomers()
        {
            //get updated customers
            var updatedCustomers = _customerService.GetCustomersByIds(_synchronizationRecordService
                .GetRecordsByEntityTypeAndActionType(EntityType.Customer, ActionType.Update).Select(record => record.EntityId).ToArray());

            return _storeService.GetAllStores().SelectMany(store => updatedCustomers.Select(customer =>
            {
                var customerOrders = _orderService.SearchOrders(storeId: store.Id, customerId: customer.Id);
                var customerCountry = _countryService.GetCountryById(customer.GetAttribute<int>(SystemCustomerAttributeNames.CountryId));
                var customerProvince = _stateProvinceService.GetStateProvinceById(customer.GetAttribute<int>(SystemCustomerAttributeNames.StateProvinceId));

                return new Operation
                {
                    Method = "PUT",
                    Path = string.Format("/ecommerce/stores/{0}/customers/{1}", store.Id, customer.Id),
                    OperationId = string.Format("update_customer_#{0}_on_store_{1}", customer.Id, store.Name),
                    Body = JsonConvert.SerializeObject(new model.Customer
                    {
                        Id = customer.Id.ToString(),
                        EmailAddress = customer.Email,
                        OptInStatus = false,
                        OrdersCount = customerOrders.TotalCount,
                        TotalSpent = (double)customerOrders.Sum(order => order.OrderTotal),
                        FirstName = customer.GetAttribute<string>(SystemCustomerAttributeNames.FirstName),
                        LastName = customer.GetAttribute<string>(SystemCustomerAttributeNames.LastName),
                        Company = customer.GetAttribute<string>(SystemCustomerAttributeNames.Company),
                        Address = new model.Address
                        {
                            Address1 = customer.GetAttribute<string>(SystemCustomerAttributeNames.StreetAddress) ?? string.Empty,
                            Address2 = customer.GetAttribute<string>(SystemCustomerAttributeNames.StreetAddress2) ?? string.Empty,
                            City = customer.GetAttribute<string>(SystemCustomerAttributeNames.City) ?? string.Empty,
                            Province = customerProvince != null ? customerProvince.Name : string.Empty,
                            ProvinceCode = customerProvince != null ? customerProvince.Abbreviation : string.Empty,
                            Country = customerCountry != null ? customerCountry.Name : string.Empty,
                            CountryCode = customerCountry != null ? customerCountry.TwoLetterIsoCode : string.Empty,
                            PostalCode = customer.GetAttribute<string>(SystemCustomerAttributeNames.ZipPostalCode) ?? string.Empty
                        }
                    }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
                };
            }));
        }

        /// <summary>
        /// Get batch of deleted customer operations
        /// </summary>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetDeletedCustomers()
        {
            return _storeService.GetAllStores().SelectMany(store =>
                _synchronizationRecordService.GetRecordsByEntityTypeAndActionType(EntityType.Customer, ActionType.Delete).Select(record => new Operation
                {
                    Method = "DELETE",
                    Path = string.Format("/ecommerce/stores/{0}/customers/{1}", store.Id, record.EntityId),
                    OperationId = string.Format("delete_customer_#{0}_from_store_{1}", record.EntityId, store.Name)
                }));
        }

        #endregion

        #region Products

        /// <summary>
        /// Get batch of product operations
        /// </summary>
        /// <returns>List of operations</returns>
        protected IList<Operation> GetProductOperations()
        {
            var productOperations = new List<Operation>();

            productOperations.AddRange(GetNewProducts());
            productOperations.AddRange(GetUpdatedProducts());
            productOperations.AddRange(GetDeletedProducts());

            //delete records
            _synchronizationRecordService.DeleteRecordsByEntityType(EntityType.Product);

            return productOperations;
        }

        /// <summary>
        /// Get batch of new product operations
        /// </summary>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetNewProducts()
        {
            //get new products
            var newProducts = _productService.GetProductsByIds(_synchronizationRecordService
                .GetRecordsByEntityTypeAndActionType(EntityType.Product, ActionType.Create).Select(record => record.EntityId).ToArray());

            return _storeService.GetAllStores().SelectMany(store => newProducts.Where(product => _storeMappingService.Authorize(product, store.Id)).Select(product =>
            {
                var productCategory = product.ProductCategories.FirstOrDefault();
                var productManufacturer = product.ProductManufacturers.FirstOrDefault();

                return new Operation
                {
                    Method = "POST",
                    Path = string.Format("/ecommerce/stores/{0}/products", store.Id),
                    OperationId = string.Format("create_product_#{0}_to_store_{1}", product.Id, store.Name),
                    Body = JsonConvert.SerializeObject(new model.Product
                    {
                        Id = product.Id.ToString(),
                        Title = product.Name,
                        Url = GetProductUrl(store, product),
                        Description = Core.Html.HtmlHelper.StripTags(GetProductDescription(product)),
                        Type = productCategory != null && productCategory.Category != null ? productCategory.Category.Name : string.Empty,
                        Vendor = productManufacturer != null && productManufacturer.Manufacturer != null ? productManufacturer.Manufacturer.Name : string.Empty,
                        ImageUrl = GetImageUrl(store, product),
                        PublishedAtForeign = product.CreatedOnUtc.ToString("s"),
                        Variants = CreateProductVariants(store, product)
                    }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
                };
            }));
        }

        /// <summary>
        /// Get batch of updated product operations
        /// </summary>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetUpdatedProducts()
        {
            //get updated products
            var updatedProducts = _productService.GetProductsByIds(_synchronizationRecordService
                .GetRecordsByEntityTypeAndActionType(EntityType.Product, ActionType.Update).Select(record => record.EntityId).ToArray());

            return _storeService.GetAllStores().SelectMany(store => updatedProducts.Where(product => _storeMappingService.Authorize(product, store.Id))
                .SelectMany(product =>
                {
                    var productCategory = product.ProductCategories.FirstOrDefault();
                    var productManufacturer = product.ProductManufacturers.FirstOrDefault();

                    //update product properties
                    var updatedProduct = new Operation
                    {
                        Method = "PATCH",
                        Path = string.Format("/ecommerce/stores/{0}/products/{1}", store.Id, product.Id),
                        OperationId = string.Format("update_product_#{0}_on_store_{1}", product.Id, store.Name),
                        Body = JsonConvert.SerializeObject(new model.Product
                        {
                            Title = product.Name,
                            Url = GetProductUrl(store, product),
                            Description = Core.Html.HtmlHelper.StripTags(GetProductDescription(product)),
                            Type = productCategory != null && productCategory.Category != null ? productCategory.Category.Name : string.Empty,
                            Vendor = productManufacturer != null && productManufacturer.Manufacturer != null ? productManufacturer.Manufacturer.Name : string.Empty,
                            ImageUrl = GetImageUrl(store, product),
                        }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
                    };

                    //update default product variant
                    var updatedProductVariant = new Operation
                    {
                        Method = "PUT",
                        Path = string.Format("/ecommerce/stores/{0}/products/{1}/variants/{2}", store.Id, product.Id, product.Id),
                        OperationId = string.Format("update_product_variant_#{0}_on_store_{1}", product.Id, store.Name),
                        Body = JsonConvert.SerializeObject(CreateDefaultProductVariant(store, product),
                            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
                    };

                    return new[] { updatedProduct, updatedProductVariant };
                }));
        }

        /// <summary>
        /// Get batch of deleted product operations
        /// </summary>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetDeletedProducts()
        {
            return _storeService.GetAllStores().SelectMany(store =>
                _synchronizationRecordService.GetRecordsByEntityTypeAndActionType(EntityType.Product, ActionType.Delete).Select(record => new Operation
                {
                    Method = "DELETE",
                    Path = string.Format("/ecommerce/stores/{0}/products/{1}", store.Id, record.EntityId),
                    OperationId = string.Format("delete_product_#{0}_from_store_{1}", record.EntityId, store.Name)
                }));
        }

        /// <summary>
        /// Create product variants for the product
        /// </summary>
        /// <param name="store">Store</param>
        /// <param name="product">Product</param>
        /// <returns>List of product variants</returns>
        protected IList<model.Variant> CreateProductVariants(Store store, Product product)
        {
            //add default variant
            var variants = new List<model.Variant> { CreateDefaultProductVariant(store, product) };

            //add variants from attributes
            variants.AddRange(CreateProductVariantsFromAttributes(store, product.ProductAttributeMappings.Where(attribute => !attribute.ShouldHaveValues())));

            //add variants from attribute values
            variants.AddRange(CreateProductVariantsFromValues(store, product.ProductAttributeMappings.Where(attribute => attribute.ShouldHaveValues()
                && attribute.ProductAttributeValues.Any()).SelectMany(attribute => attribute.ProductAttributeValues)));

            //add variants from attribute combinations
            variants.AddRange(CreateProductVariantsFromCombinations(store, product.ProductAttributeCombinations));

            return variants;
        }

        /// <summary>
        /// Create default variant for the product (each product must have at least one variant)
        /// </summary>
        /// <param name="store">Store</param>
        /// <param name="product">Product</param>
        /// <returns>Default product variant</returns>
        protected model.Variant CreateDefaultProductVariant(Store store, Product product)
        {
            var price =  (double)product.Price;
            var quantity = product.ManageInventoryMethod != ManageInventoryMethod.DontManageStock ? product.StockQuantity : int.MaxValue;
            var imageUrl = GetImageUrl(store, product);
            var url = GetProductUrl(store, product);

            //default product variant (copy of product properties)
            return CreateProductVariant(product.Id.ToString(), product.Name, url, product.Sku, price, quantity, imageUrl, product.Published);
        }

        /// <summary>
        /// Create product variants from product attributes that should not have values
        /// </summary>
        /// <param name="store">Store</param>
        /// <param name="attributes">Collection of product attributes</param>
        /// <returns>Collection of product variants</returns>
        protected IEnumerable<model.Variant> CreateProductVariantsFromAttributes(Store store, IEnumerable<ProductAttributeMapping> attributes)
        {
            return attributes.Where(attribute => !attribute.ShouldHaveValues() && attribute.ProductAttribute != null && attribute.Product != null)
                .Select(attribute =>
                {
                    var imageUrl = GetImageUrl(store, attribute.Product);
                    var url = GetProductUrl(store, attribute.Product);
                    var title = attribute.ProductAttribute != null
                        ? string.Format("{0} {1}", attribute.Product.Name, attribute.ProductAttribute.Name) : attribute.Product.Name;
                    var price = (double)attribute.Product.Price;
                    var quantity = attribute.Product.ManageInventoryMethod != ManageInventoryMethod.DontManageStock ? attribute.Product.StockQuantity : int.MaxValue;

                    return CreateProductVariant(string.Format("{0}_a_{1}", attribute.ProductId, attribute.Id), title, url, attribute.Product.Sku, price,
                        quantity, imageUrl, attribute.Product.Published);
                });
        }

        /// <summary>
        /// Create product variants from product attribute values (each value as the distinct variant)
        /// </summary>
        /// <param name="store">Store</param>
        /// <param name="values">Collection of product attribute values</param>
        /// <returns>Collection of product variants</returns>
        protected IEnumerable<model.Variant> CreateProductVariantsFromValues(Store store, IEnumerable<ProductAttributeValue> values)
        {
            return values.Where(value => value.ProductAttributeMapping != null && value.ProductAttributeMapping.ProductAttribute != null
                && value.ProductAttributeMapping.Product != null).Select(value =>
                {
                    var url = GetProductUrl(store, value.ProductAttributeMapping.Product);
                    var valueQuantity = value.AttributeValueType == AttributeValueType.AssociatedToProduct ? value.Quantity
                        : value.ProductAttributeMapping.Product.ManageInventoryMethod != ManageInventoryMethod.DontManageStock
                        ? value.ProductAttributeMapping.Product.StockQuantity : int.MaxValue;
                    var title = string.Format("{0} {1}", value.ProductAttributeMapping.Product.Name, value.Name);
                    var valuePrice = (double)(value.ProductAttributeMapping.Product.Price
                        + _priceCalculationService.GetProductAttributeValuePriceAdjustment(value));
                    var valueImageUrl = value.ImageSquaresPictureId > 0
                        ? _pictureService.GetPictureUrl(_pictureService.GetPictureById(value.ImageSquaresPictureId))
                        : GetImageUrl(store, value.ProductAttributeMapping.Product);

                    return CreateProductVariant(string.Format("{0}_v_{1}", value.ProductAttributeMapping.ProductId, value.Id), title, url,
                        value.ProductAttributeMapping.Product.Sku, valuePrice, valueQuantity, valueImageUrl, value.ProductAttributeMapping.Product.Published);
                });
        }

        /// <summary>
        /// Create product variants from product attribute combinations
        /// </summary>
        /// <param name="store">Store</param>
        /// <param name="combinations">Collection of product attribute combinations</param>
        /// <returns>Collection of product variants</returns>
        protected IEnumerable<model.Variant> CreateProductVariantsFromCombinations(Store store, IEnumerable<ProductAttributeCombination> combinations)
        {
            return combinations.Where(combination => combination.Product != null).Select(combination =>
            {
                var imageUrl = GetImageUrl(store, combination.Product);
                var url = GetProductUrl(store, combination.Product);
                var combinationQuantity = combination.Product.ManageInventoryMethod == ManageInventoryMethod.ManageStockByAttributes ? combination.StockQuantity
                    : combination.Product.ManageInventoryMethod != ManageInventoryMethod.DontManageStock ? combination.Product.StockQuantity : int.MaxValue;
                var sku = !string.IsNullOrEmpty(combination.Sku) ? combination.Sku : combination.Product.Sku;
                var combinationPrice = (double)(combination.OverriddenPrice ?? combination.Product.Price);

                return CreateProductVariant(string.Format("{0}_c_{1}", combination.ProductId, combination.Id), combination.Product.Name, url, sku,
                    combinationPrice, combinationQuantity, imageUrl, combination.Product.Published);
            });
        }

        /// <summary>
        /// Create product variant for MailChimp Ecommerce API
        /// </summary>
        /// <param name="id">Identifier</param>
        /// <param name="title">Title</param>
        /// <param name="url">URL</param>
        /// <param name="sku">SKU</param>
        /// <param name="price">Price</param>
        /// <param name="quantity">Quantity</param>
        /// <param name="imageUrl">URL of the image</param>
        /// <param name="published">Value indicating whether product is published</param>
        /// <returns>Product variant</returns>
        protected model.Variant CreateProductVariant(string id, string title, string url, string sku, double price, int quantity, string imageUrl, bool published)
        {
            return new model.Variant
            {
                Id = id,
                Title = title,
                Url = url,
                Sku = sku,
                Price = price,
                InventoryQuantity = quantity,
                ImageUrl = imageUrl,
                Visibility = published.ToString().ToLowerInvariant()
            };
        }

        /// <summary>
        /// Get description of the product
        /// </summary>
        /// <param name="product">Product</param>
        /// <returns>Product description</returns>
        protected string GetProductDescription(Product product)
        {
            return !string.IsNullOrEmpty(product.FullDescription) ? product.FullDescription
                : !string.IsNullOrEmpty(product.ShortDescription) ? product.ShortDescription : product.Name;
        }

        /// <summary>
        /// Get URL of the product
        /// </summary>
        /// <param name="store">Store</param>
        /// <param name="product">Product</param>
        /// <returns>URL</returns>
        protected string GetProductUrl(Store store, Product product)
        {
            return string.Format("{0}{1}", _webHelper.GetStoreLocation(store.SslEnabled), product.GetSeName());
        }

        /// <summary>
        /// Get URL of the image
        /// </summary>
        /// <param name="store">Store</param>
        /// <param name="product">Product</param>
        /// <returns>Image URL</returns>
        protected string GetImageUrl(Store store, Product product)
        {
            var productPicture = product.ProductPictures.FirstOrDefault();
            return productPicture != null && productPicture.Picture != null
                ? _pictureService.GetPictureUrl(productPicture.Picture, storeLocation: store.Url)
                : _pictureService.GetDefaultPictureUrl(storeLocation: store.Url);
        }

        #region Product variants

        /// <summary>
        /// Get batch of product variant operations
        /// </summary>
        /// <returns>List of operations</returns>
        protected IList<Operation> GetProductVariantOperations()
        {
            var productVariantOperations = new List<Operation>();

            productVariantOperations.AddRange(GetNewProductVariants());
            productVariantOperations.AddRange(GetUpdatedProductVariants());
            productVariantOperations.AddRange(GetDeletedProductVariants());

            //delete records
            _synchronizationRecordService.DeleteRecordsByEntityType(EntityType.ProductAttribute);
            _synchronizationRecordService.DeleteRecordsByEntityType(EntityType.AttributeValue);
            _synchronizationRecordService.DeleteRecordsByEntityType(EntityType.AttributeCombination);

            return productVariantOperations;
        }

        /// <summary>
        /// Get batch of new product variant operations
        /// </summary>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetNewProductVariants()
        {
            //new attributes
            var newAttributes = _synchronizationRecordService.GetRecordsByEntityTypeAndActionType(EntityType.ProductAttribute, ActionType.Create)
                .Select(record => _productAttributeService.GetProductAttributeMappingById(record.EntityId));

            //new attribute values
            var newValues = _synchronizationRecordService.GetRecordsByEntityTypeAndActionType(EntityType.AttributeValue, ActionType.Create)
                .Select(record => _productAttributeService.GetProductAttributeValueById(record.EntityId));

            //new attribute combinations
            var newCombinations = _synchronizationRecordService.GetRecordsByEntityTypeAndActionType(EntityType.AttributeCombination, ActionType.Create)
                .Select(record => _productAttributeService.GetProductAttributeCombinationById(record.EntityId));

            return _storeService.GetAllStores().SelectMany(store =>
            {
                var variants = new List<model.Variant>();
                variants.AddRange(CreateProductVariantsFromAttributes(store,
                    newAttributes.Where(attribute => _storeMappingService.Authorize(attribute.Product, store.Id))));
                variants.AddRange(CreateProductVariantsFromValues(store,
                    newValues.Where(value => value.ProductAttributeMapping != null
                    && _storeMappingService.Authorize(value.ProductAttributeMapping.Product, store.Id))));
                variants.AddRange(CreateProductVariantsFromCombinations(store,
                    newCombinations.Where(combination => _storeMappingService.Authorize(combination.Product, store.Id))));

                return variants.Select(variant => new Operation
                {
                    Method = "POST",
                    Path = string.Format("/ecommerce/stores/{0}/products/{1}/variants", store.Id, variant.Id.Split('_').First()),
                    OperationId = string.Format("create_product_variant_#{0}_to_store_{1}", variant.Id, store.Name),
                    Body = JsonConvert.SerializeObject(variant, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
                });
            });
        }

        /// <summary>
        /// Get batch of updated product variant operations
        /// </summary>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetUpdatedProductVariants()
        {
            //updated attributes
            var updatedAttributes = _synchronizationRecordService.GetRecordsByEntityTypeAndActionType(EntityType.ProductAttribute, ActionType.Update)
                .Select(record => _productAttributeService.GetProductAttributeMappingById(record.EntityId));

            //updated attribute values
            var updatedValues = _synchronizationRecordService.GetRecordsByEntityTypeAndActionType(EntityType.AttributeValue, ActionType.Update)
                .Select(record => _productAttributeService.GetProductAttributeValueById(record.EntityId));

            //updated attribute combinations
            var updatedCombinations = _synchronizationRecordService.GetRecordsByEntityTypeAndActionType(EntityType.AttributeCombination, ActionType.Update)
                .Select(record => _productAttributeService.GetProductAttributeCombinationById(record.EntityId));

            return _storeService.GetAllStores().SelectMany(store =>
            {
                var variants = new List<model.Variant>();
                variants.AddRange(CreateProductVariantsFromAttributes(store,
                    updatedAttributes.Where(attribute => _storeMappingService.Authorize(attribute.Product, store.Id))));
                variants.AddRange(CreateProductVariantsFromValues(store,
                    updatedValues.Where(value => value.ProductAttributeMapping != null
                    && _storeMappingService.Authorize(value.ProductAttributeMapping.Product, store.Id))));
                variants.AddRange(CreateProductVariantsFromCombinations(store,
                    updatedCombinations.Where(combination => _storeMappingService.Authorize(combination.Product, store.Id))));

                return variants.Select(variant => new Operation
                {
                    Method = "PATCH",
                    Path = string.Format("/ecommerce/stores/{0}/products/{1}/variants/{2}", store.Id, variant.Id.Split('_').First(), variant.Id),
                    OperationId = string.Format("update_product_variant_#{0}_on_store_{1}", variant.Id, store.Name),
                    Body = JsonConvert.SerializeObject(variant, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
                });
            });
        }

        /// <summary>
        /// Get batch of deleted product variant operations
        /// </summary>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetDeletedProductVariants()
        {
            //deleted attributes
            var deletedAttributes = _synchronizationRecordService.GetRecordsByEntityTypeAndActionType(EntityType.ProductAttribute, ActionType.Delete);

            //deleted attribute values
            var deletedValues = _synchronizationRecordService.GetRecordsByEntityTypeAndActionType(EntityType.AttributeValue, ActionType.Delete);

            //deleted combinations
            var deletedCombinations = _synchronizationRecordService.GetRecordsByEntityTypeAndActionType(EntityType.AttributeCombination, ActionType.Delete);

            return _storeService.GetAllStores().SelectMany(store =>
            {
                var operations = new List<Operation>();

                //delete attributes
                operations.AddRange(deletedAttributes.Select(attribute => new Operation
                {
                    Method = "DELETE",
                    Path = string.Format("/ecommerce/stores/{0}/products/{1}/variants/{1}_a_{2}", store.Id, attribute.ProductId, attribute.Id),
                    OperationId = string.Format("delete_product_variant_#{0}_a_{1}_from_store_{2}", attribute.ProductId, attribute.Id, store.Name)
                }));

                //delete attribute values
                operations.AddRange(deletedValues.Select(value => new Operation
                {
                    Method = "DELETE",
                    Path = string.Format("/ecommerce/stores/{0}/products/{1}/variants/{1}_v_{2}", store.Id, value.ProductId, value.Id),
                    OperationId = string.Format("delete_product_variant_#{0}_v_{1}_from_store_{2}", value.ProductId, value.Id, store.Name)
                }));

                //delete attribute combinations
                operations.AddRange(deletedCombinations.Select(combination => new Operation
                {
                    Method = "DELETE",
                    Path = string.Format("/ecommerce/stores/{0}/products/{1}/variants/{1}_c_{2}", store.Id, combination.ProductId, combination.Id),
                    OperationId = string.Format("delete_product_variant_#{0}_c_{1}_from_store_{2}", combination.ProductId, combination.Id, store.Name)
                }));

                return operations;
            });
        }

        #endregion

        #endregion

        #region Orders

        /// <summary>
        /// Get batch of order operations
        /// </summary>
        /// <returns>List of operations</returns>
        protected IList<Operation> GetOrderOperations()
        {
            var orderOperations = new List<Operation>();

            orderOperations.AddRange(GetNewOrders());
            orderOperations.AddRange(GetUpdatedOrders());
            orderOperations.AddRange(GetDeletedOrders());

            //delete records
            _synchronizationRecordService.DeleteRecordsByEntityType(EntityType.Order);

            return orderOperations;
        }

        /// <summary>
        /// Get batch of new order operations
        /// </summary>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetNewOrders()
        {
            var currency = GetCurrencyCode();

            //get new orders
            var newOrders = _orderService.GetOrdersByIds(_synchronizationRecordService
                .GetRecordsByEntityTypeAndActionType(EntityType.Order, ActionType.Create).Select(record => record.EntityId).ToArray());

            return _storeService.GetAllStores().SelectMany(store => newOrders.Where(order => order.StoreId == store.Id).Select(order =>
            {
                //billing address
                var billingAddress = order.BillingAddress != null ? CreateAddress(order.BillingAddress) : null;

                //shipping address or pickup address
                var shippingAddress = !order.PickUpInStore && order.ShippingAddress != null ? CreateAddress(order.ShippingAddress)
                    : order.PickUpInStore && order.PickupAddress != null ? CreateAddress(order.PickupAddress) : null;

                return new Operation
                {
                    Method = "POST",
                    Path = string.Format("/ecommerce/stores/{0}/orders", store.Id),
                    OperationId = string.Format("create_order_#{0}_to_store_{1}", order.Id, store.Name),
                    Body = JsonConvert.SerializeObject(new model.Order
                    {
                        Id = order.Id.ToString(),
                        Customer = new model.Customer { Id = order.Customer.Id.ToString() },
                        FinancialStatus = order.PaymentStatus.ToString(),
                        FulfillmentStatus = order.OrderStatus.ToString(),
                        CurrencyCode = currency,
                        OrderTotal = (double)order.OrderTotal,
                        TaxTotal = (double)order.OrderTax,
                        ShippingTotal = (double)order.OrderShippingInclTax,
                        ProcessedAtForeign = order.CreatedOnUtc.ToString("s"),
                        ShippingAddress = shippingAddress,
                        BillingAddress = billingAddress,
                        Lines = CreateOrderLines(order.OrderItems)
                    }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
                };
            }));
        }

        /// <summary>
        /// Get batch of updated order operations
        /// </summary>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetUpdatedOrders()
        {
            var currency = GetCurrencyCode();

            //get updated orders
            var updatedOrders = _orderService.GetOrdersByIds(_synchronizationRecordService
                .GetRecordsByEntityTypeAndActionType(EntityType.Order, ActionType.Update).Select(record => record.EntityId).ToArray());

            return _storeService.GetAllStores().SelectMany(store => updatedOrders.Where(order => order.StoreId == store.Id).Select(order =>
            {
                //billing address
                var billingAddress = order.BillingAddress != null ? CreateAddress(order.BillingAddress) : null;

                //shipping address or pickup address
                var shippingAddress = !order.PickUpInStore && order.ShippingAddress != null ? CreateAddress(order.ShippingAddress)
                    : order.PickUpInStore && order.PickupAddress != null ? CreateAddress(order.PickupAddress) : null;

                return new Operation
                {
                    Method = "PATCH",
                    Path = string.Format("/ecommerce/stores/{0}/orders/{1}", store.Id, order.Id),
                    OperationId = string.Format("update_order_#{0}_on_store_{1}", order.Id, store.Name),
                    Body = JsonConvert.SerializeObject(new model.Order
                    {
                        Customer = new model.Customer { Id = order.Customer.Id.ToString() },
                        FinancialStatus = order.PaymentStatus.ToString(),
                        FulfillmentStatus = order.OrderStatus.ToString(),
                        CurrencyCode = currency,
                        OrderTotal = (double)order.OrderTotal,
                        TaxTotal = (double)order.OrderTax,
                        ShippingTotal = (double)order.OrderShippingInclTax,
                        ProcessedAtForeign = order.CreatedOnUtc.ToString("s"),
                        ShippingAddress = shippingAddress,
                        BillingAddress = billingAddress,
                        Lines = CreateOrderLines(order.OrderItems)
                    }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
                };
            }));
        }

        /// <summary>
        /// Get batch of deleted order operations
        /// </summary>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetDeletedOrders()
        {
            //get deleted orders
            var deletedOrders = _orderService.GetOrdersByIds(_synchronizationRecordService
                .GetRecordsByEntityTypeAndActionType(EntityType.Order, ActionType.Delete).Select(record => record.EntityId).ToArray());

            return _storeService.GetAllStores().SelectMany(store => deletedOrders.Where(order => order.StoreId == store.Id)
                .Select(order => new Operation
                {
                    Method = "DELETE",
                    Path = string.Format("/ecommerce/stores/{0}/orders{1}", store.Id, order.Id),
                    OperationId = string.Format("delete_order_#{0}_from_store_{1}", order.Id, store.Name)
                }));
        }

        /// <summary>
        /// Create address for MailChimp Ecommerce API
        /// </summary>
        /// <param name="address">Address (nopCommerce object)</param>
        /// <returns>Address</returns>
        protected model.Address CreateAddress(Address address)
        {
            return new model.Address
            {
                Address1 = address.Address1 ?? string.Empty,
                Address2 = address.Address2 ?? string.Empty,
                City = address.City ?? string.Empty,
                Province = address.StateProvince != null ? address.StateProvince.Name : string.Empty,
                ProvinceCode = address.StateProvince != null ? address.StateProvince.Abbreviation : string.Empty,
                Country = address.Country != null ? address.Country.Name : string.Empty,
                CountryCode = address.Country != null ? address.Country.TwoLetterIsoCode : string.Empty,
                PostalCode = address.ZipPostalCode ?? string.Empty,
            };
        }

        /// <summary>
        /// Create orders lines from order items
        /// </summary>
        /// <param name="items">Collection of order items</param>
        /// <returns>List of order lines</returns>
        protected IList<model.Line> CreateOrderLines(IEnumerable<OrderItem> items)
        {
            return items.Select(item => new model.Line
            {
                Id = item.Id.ToString(),
                ProductId = item.ProductId.ToString(),
                ProductVariantId = GetProductVariantId(item.Product, item.AttributesXml),
                Price = (double)item.PriceInclTax,
                Quantity = item.Quantity
            }).ToList();
        }

        /// <summary>
        /// Get product variant identifier
        /// </summary>
        /// <param name="product">Product</param>
        /// <param name="attributesXml">Product atrributes (XML format)</param>
        /// <returns>Product variant identifier</returns>
        protected string GetProductVariantId(Product product, string attributesXml)
        {
            //first set identifier to default
            var variantId = product != null ? product.Id.ToString() : string.Empty;

            //try get combination
            var combination = _productAttributeParser.FindProductAttributeCombination(product, attributesXml);
            if (combination != null)
                variantId = string.Format("{0}_c_{1}", product.Id, combination.Id);
            else
            {
                //or try get certain attribute value
                var values = _productAttributeParser.ParseProductAttributeValues(attributesXml);
                if (values.Any())
                    variantId = string.Format("{0}_v_{1}", product.Id, values.First().Id);
                else
                {
                    //or try get attribute
                    var attributes = _productAttributeParser.ParseProductAttributeMappings(attributesXml);
                    if (attributes.Any())
                        variantId = string.Format("{0}_a_{1}", product.Id, attributes.First().Id);
                }
            }

            return variantId;
        }

        #endregion

        #region Carts

        /// <summary>
        /// Get batch of cart operations
        /// </summary>
        /// <returns>List of operations</returns>
        protected async Task<IList<Operation>> GetCartOperations()
        {
            var cartOperations = new List<Operation>();

            foreach (var store in _storeService.GetAllStores())
            {
                //get customers with shopping cart
                //customer ID in nop equals cart ID in MailChimp
                var customers = _customerService.GetAllCustomers(loadOnlyWithShoppingCart: true, sct: ShoppingCartType.ShoppingCart)
                    .Where(customer => customer.ShoppingCartItems.Any(cart => cart.StoreId == store.Id)).ToList();

                var cartIds = new List<string>();

                //get existing carts on MailChimp
                try
                {
                    var carts = await Manager.ECommerceStores.Carts(store.Id.ToString())
                        .GetAllAsync(new QueryableBaseRequest { FieldsToInclude = "carts.id" });
                    cartIds = carts.Select(cart => cart.Id).ToList();
                }
                catch (MailChimpNotFoundException) { }
                
                cartOperations.AddRange(GetNewCarts(store, customers.Where(customer => !cartIds.Contains(customer.Id.ToString()))));
                cartOperations.AddRange(GetUpdatedCarts(store, customers.Where(customer => cartIds.Contains(customer.Id.ToString()))));
                cartOperations.AddRange(GetDeletedCarts(store, cartIds.Except(customers.Select(customer => customer.Id.ToString()))));
            }

            return cartOperations;
        }

        /// <summary>
        /// Get batch of new cart operations
        /// </summary>
        /// <param name="store">Store</param>
        /// <param name="customers">Collections of customers with uncompleted cart</param>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetNewCarts(Store store, IEnumerable<Customer> customers)
        {
            var currency = GetCurrencyCode();

            return customers.Select(customer =>
            {
                var lines = CreateCartLines(customer.ShoppingCartItems.LimitPerStore(store.Id));

                return new Operation
                {
                    Method = "POST",
                    Path = string.Format("/ecommerce/stores/{0}/carts", store.Id),
                    OperationId = string.Format("create_cart_#{0}_to_store_{1}", customer.Id, store.Name),
                    Body = JsonConvert.SerializeObject(new model.Cart
                    {
                        Id = customer.Id.ToString(),
                        Customer = new model.Customer { Id = customer.Id.ToString() },
                        CheckoutUrl = string.Format("{0}cart/", _webHelper.GetStoreLocation(store.SslEnabled)),
                        CurrencyCode = currency,
                        OrderTotal = lines.Sum(line => line.Price),
                        Lines = lines
                    }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
                };
            });
        }

        /// <summary>
        /// Get batch of updated cart operations
        /// </summary>
        /// <param name="store">Store</param>
        /// <param name="customers">Collections of customers with uncompleted cart</param>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetUpdatedCarts(Store store, IEnumerable<Customer> customers)
        {
            var currency = GetCurrencyCode();

            return customers.Select(customer =>
            {
                var lines = CreateCartLines(customer.ShoppingCartItems.LimitPerStore(store.Id));

                return new Operation
                {
                    Method = "PATCH",
                    Path = string.Format("/ecommerce/stores/{0}/carts/{1}", store.Id, customer.Id),
                    OperationId = string.Format("update_cart_#{0}_on_store_{1}", customer.Id, store.Name),
                    Body = JsonConvert.SerializeObject(new model.Cart
                    {
                        Customer = new model.Customer { Id = customer.Id.ToString() },
                        CurrencyCode = currency,
                        OrderTotal = lines.Sum(line => line.Price),
                        Lines = lines
                    }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
                };
            });
        }

        /// <summary>
        /// Get batch of deleted cart operations
        /// </summary>
        /// <param name="store">Store</param>
        /// <param name="customerIds">Collections of customer identifiers</param>
        /// <returns>Collection of operations</returns>
        protected IEnumerable<Operation> GetDeletedCarts(Store store, IEnumerable<string> customerIds)
        {
            return customerIds.Select(id => new Operation
            {
                Method = "DELETE",
                Path = string.Format("/ecommerce/stores/{0}/carts/{1}", store.Id, id),
                OperationId = string.Format("delete_cart_#{0}_from_store_{1}", id, store.Name)
            });
        }

        /// <summary>
        /// Create cart lines from shopping cart items
        /// </summary>
        /// <param name="items">Collection of shopping cart items</param>
        /// <returns>Collection of cart lines</returns>
        protected IList<model.Line> CreateCartLines(IEnumerable<ShoppingCartItem> items)
        {
            return items.Select(item => new model.Line
            {
                Id = item.Id.ToString(),
                ProductId = item.ProductId.ToString(),
                ProductVariantId = GetProductVariantId(item.Product, item.AttributesXml),
                Price = (double)_priceCalculationService.GetSubTotal(item),
                Quantity = item.Quantity
            }).ToList();
        }

        #endregion

        #endregion

        #endregion

        #region Methods

        /// <summary>
        /// Create data for the first synchronization
        /// </summary>
        public void CreateInitiateData()
        {
            //add all subscriptions
            foreach (var subscription in _newsLetterSubscriptionService.GetAllNewsLetterSubscriptions())
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Subscription, subscription.Id, ActionType.Create);

            //add stores
            foreach (var store in _storeService.GetAllStores())
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Store, store.Id, ActionType.Create);

            //add not guests customers
            foreach (var customer in _customerService.GetAllCustomers().Where(customer => !customer.IsGuest()))
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Customer, customer.Id, ActionType.Create);

            //add products
            foreach (var product in _productService.SearchProducts())
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Product, product.Id, ActionType.Create);

            //add orders
            foreach (var order in _orderService.SearchOrders())
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Order, order.Id, ActionType.Create);
        }

        /// <summary>
        /// Synchronize nopCommerce data with MailChimp
        /// </summary>
        /// <returns>Batch identifier</returns>
        public async Task<string> Synchronize()
        {
            if (!IsConfigured)
                return string.Empty;

            var mailChimpSettings = _settingService.LoadSetting<MailChimpSettings>();

            //manage subscriptions
            var operations = new List<Operation>(GetSubscriptionOperations(mailChimpSettings));

            //manage Ecommerce resources
            if (mailChimpSettings.UseEcommerceApi)
                operations.AddRange(await GetEcommerceApiOperations(mailChimpSettings));

            //send request
            var batch = await Manager.Batches.AddAsync(new BatchRequest { Operations = operations });
            
            //log information after the batch operation is complete
            LogAfterComplete(batch);

            return batch.Id;
        }

        /// <summary>
        /// Get MailChimp account information
        /// </summary>
        /// <returns>Account information</returns>
        public async Task<string> GetAccountInfo()
        {
            if (!IsConfigured)
                return string.Empty;

            var apiInfo = await Manager.Api.GetInfoAsync();

            return string.Format("{0}{1}Total subscribers: {2}", apiInfo.AccountName, Environment.NewLine, apiInfo.TotalSubscribers);
        }

        /// <summary>
        /// Get available lists of contacts for the synchronization
        /// </summary>
        /// <returns>List of lists</returns>
        public async Task<IList<SelectListItem>> GetAvailableLists()
        {
            var result = new List<SelectListItem> { new SelectListItem { Text = "<Select list>", Value = "0" } };

            if (!IsConfigured)
                return result;

            var totalItems = (await Manager.Lists.GetResponseAsync()).TotalItems;
            var availableLists = await Manager.Lists.GetAllAsync(new ListRequest { Limit = totalItems });
            result.AddRange(availableLists.Select(list => new SelectListItem { Text = list.Name, Value = list.Id }));

            return result;
        }

        /// <summary>
        /// Create webhook for the subscribe and unsubscribe events
        /// </summary>
        /// <param name="listId">List identifier</param>
        /// <param name="currentWebhookId">Current webhook identifier, if exists</param>
        /// <returns>Webhook identifier</returns>
        public async Task<string> CreateWebhook(string listId, string currentWebhookId)
        {
            if (!IsConfigured)
                return string.Empty;

            try
            {
                //check if already exists
                if (!string.IsNullOrEmpty(currentWebhookId))
                    return (await Manager.WebHooks.GetAsync(listId, currentWebhookId)).Id;
            }
            catch (MailChimpNotFoundException) { }

            //create new one
            var url = $"{_webHelper.GetStoreLocation(_storeContext.CurrentStore.SslEnabled)}Plugins/MailChimp/Webhook";
            var batch = await Manager.Batches.AddAsync(new BatchRequest
            {
                Operations = new List<Operation>
                {
                    new Operation
                    {
                        Method = "POST",
                        Path = $"/lists/{listId}/webhooks",
                        OperationId = $"create_webhook_to_list_#{listId}",
                        Body = JsonConvert.SerializeObject(new WebHook
                        {
                            ListId = listId,
                            Event = new model.Event { Unsubscribe = true, Subscribe = true },
                            Source = new model.Source { Admin = true, User = true },
                            Url = url
                        }, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
                    }
                }
            });

            do
            {
                await Task.Delay(1000);
                batch = await Manager.Batches.GetBatchStatus(batch.Id);
            } while (!BatchOperationIsComplete(batch));

            var newWebhook = (await Manager.WebHooks.GetAllAsync(listId)).FirstOrDefault(webhook => webhook.Url.Equals(url));
            if (newWebhook != null)
                return newWebhook.Id;

            _logger.Error("MailChimp error: webhook was not created");
            return string.Empty;
        }

        /// <summary>
        /// Delete webhook from MailChimp
        /// </summary>
        /// <param name="listId">List identifier</param>
        /// <param name="currentWebhookId">Webhook identifier</param>
        public void DeleteWebhook(string listId, string currentWebhookId)
        {
            if (!IsConfigured)
                return;

            try
            {
                Manager.WebHooks.DeleteAsync(listId, currentWebhookId);
            }
            catch (MailChimpNotFoundException) { }
        }

        /// <summary>
        /// Get information about batch operations
        /// </summary>
        /// <param name="batchId">Batch identifier</param>
        /// <returns>True if batch is completed, otherwise false; information about completed operations</returns>
        public async Task<Tuple<bool, string>> GetBatchInfo(string batchId)
        {
            //we use Tuple because the async methods cannot be used with out parameters
            if (!IsConfigured)
                return new Tuple<bool, string>(true, string.Empty);

            var batch = await Manager.Batches.GetBatchStatus(batchId);
            var info = $"Started at: {batch.SubmittedAt}{Environment.NewLine}Finished operations: {batch.FinishedOperations}{Environment.NewLine}Errored operations: {batch.ErroredOperations}{Environment.NewLine}Total operations: {batch.TotalOperations}{Environment.NewLine}";

            return BatchOperationIsComplete(batch)
                ? new Tuple<bool, string>(true, $"{info}Completed at: {batch.CompletedAt}") : new Tuple<bool, string>(false, info);
        }

        /// <summary>
        /// Subscribe or unsubscribe particular email
        /// </summary>
        /// <param name="form">Input parameters</param>
        public void WebhookHandler(IFormCollection form)
        {
            if (!IsConfigured)
                return;
            
            if (string.IsNullOrEmpty(form["data[list_id]"]) || string.IsNullOrEmpty(form["type"]))
                return;

            foreach (var store in _storeService.GetAllStores())
            {
                var listId = _settingService.GetSettingByKey<string>("mailchimpsettings.listid", storeId: store.Id, loadSharedValueIfNotFound: true) ?? string.Empty;
                if (!listId.Equals(form["data[list_id]"]))
                    continue;

                //get subscription by email and store identifier
                var subscription = _newsLetterSubscriptionService.GetNewsLetterSubscriptionByEmailAndStoreId(form["data[email]"], store.Id);
                switch (form["type"].ToString().ToLowerInvariant())
                {
                    case "unsubscribe":
                        if (subscription != null)
                        {
                            //deactivate subscription
                            subscription.Active = false;
                            _newsLetterSubscriptionService.UpdateNewsLetterSubscription(subscription, false);
                            _logger.Information($"MailChimp: email {form["data[email]"]} unsubscribed from store {store.Name}");
                        }
                        break;
                    case "subscribe":
                        //if subscription already exists just activate
                        if (subscription != null)
                        {
                            subscription.Active = true;
                            _newsLetterSubscriptionService.UpdateNewsLetterSubscription(subscription, false);
                        }
                        else
                            //or create new one
                            _newsLetterSubscriptionService.InsertNewsLetterSubscription(new NewsLetterSubscription
                            {
                                NewsLetterSubscriptionGuid = Guid.NewGuid(),
                                Email = form["data[email]"],
                                StoreId = store.Id,
                                Active = true,
                                CreatedOnUtc = DateTime.UtcNow
                            }, false);
                        _logger.Information($"MailChimp: email {form["data[email]"]} subscribed to store {store.Name}");
                        break;
                }
            }
        }

        #endregion
    }
}
