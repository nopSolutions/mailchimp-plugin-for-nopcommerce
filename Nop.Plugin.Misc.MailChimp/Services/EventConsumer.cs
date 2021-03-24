using System.Linq;
using System.Threading.Tasks;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Core.Events;
using Nop.Plugin.Misc.MailChimp.Domain;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Events;

namespace Nop.Plugin.Misc.MailChimp.Services
{
    /// <summary>
    /// Represents an event consumer that prepares records for the synchronization
    /// </summary>
    public class EventConsumer :
        //stores
        IConsumer<EntityInsertedEvent<Store>>,
        IConsumer<EntityUpdatedEvent<Store>>,
        IConsumer<EntityDeletedEvent<Store>>,
        //customers
        IConsumer<CustomerRegisteredEvent>,
        IConsumer<EntityInsertedEvent<Customer>>,
        IConsumer<EntityUpdatedEvent<Customer>>,
        //subscriptions
        IConsumer<EmailUnsubscribedEvent>,
        IConsumer<EntityInsertedEvent<NewsLetterSubscription>>,
        IConsumer<EntityUpdatedEvent<NewsLetterSubscription>>,
        IConsumer<EntityDeletedEvent<NewsLetterSubscription>>,
        //products
        IConsumer<EntityInsertedEvent<Product>>,
        IConsumer<EntityUpdatedEvent<Product>>,
        //product attribute
        IConsumer<EntityDeletedEvent<ProductAttributeMapping>>,
        IConsumer<EntityDeletedEvent<ProductAttribute>>,
        //attribute values
        IConsumer<EntityUpdatedEvent<ProductAttributeValue>>,
        IConsumer<EntityDeletedEvent<ProductAttributeValue>>,
        //attribute combinations
        IConsumer<EntityInsertedEvent<ProductAttributeCombination>>,
        IConsumer<EntityUpdatedEvent<ProductAttributeCombination>>,
        IConsumer<EntityDeletedEvent<ProductAttributeCombination>>,
        //orders
        IConsumer<EntityInsertedEvent<Order>>,
        IConsumer<EntityUpdatedEvent<Order>>
    {
        #region Fields

        private readonly ICustomerService _customerService;
        private readonly IProductAttributeParser _productAttributeParser;
        private readonly IProductAttributeService _productAttributeService;
        private readonly IProductService _productService;
        private readonly ISynchronizationRecordService _synchronizationRecordService;

        #endregion

        #region Ctor

        public EventConsumer(ICustomerService customerService, 
            IProductAttributeParser productAttributeParser,
            IProductAttributeService productAttributeService,
            IProductService productService,
            ISynchronizationRecordService synchronizationRecordService)
        {
            _customerService = customerService;
            _productAttributeParser = productAttributeParser;
            _productAttributeService = productAttributeService;
            _productService = productService;
            _synchronizationRecordService = synchronizationRecordService;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Create or update the synchronization record with passed parameters
        /// </summary>
        /// <param name="entityType">Entity type</param>
        /// <param name="id">Entity identifier</param>
        /// <param name="operationType">Operation type</param>
        /// <param name="email">Subscription email</param>
        /// <param name="productId">Product identifier</param>
        private void AddRecord(EntityType entityType, int? id, OperationType operationType, string email = null, int? productId = null)
        {
            _synchronizationRecordService.CreateOrUpdateRecordAsync(entityType, id ?? 0, operationType, email, productId ?? 0);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Handle the store inserted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public Task HandleEventAsync(EntityInsertedEvent<Store> eventMessage)
        {
            if (eventMessage.Entity != null)
                AddRecord(EntityType.Store, eventMessage.Entity.Id, OperationType.Create);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle the store updated event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public Task HandleEventAsync(EntityUpdatedEvent<Store> eventMessage)
        {
            if (eventMessage.Entity != null)
                AddRecord(EntityType.Store, eventMessage.Entity.Id, OperationType.Update);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle the store deleted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public Task HandleEventAsync(EntityDeletedEvent<Store> eventMessage)
        {
            if (eventMessage.Entity != null)
                AddRecord(EntityType.Store, eventMessage.Entity.Id, OperationType.Delete);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle the customer registered event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public Task HandleEventAsync(CustomerRegisteredEvent eventMessage)
        {
            if (eventMessage.Customer != null)
                AddRecord(EntityType.Customer, eventMessage.Customer.Id, OperationType.Create);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle the customer inserted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task HandleEventAsync(EntityInsertedEvent<Customer> eventMessage)
        {
            if (eventMessage.Entity == null || await _customerService.IsGuestAsync(eventMessage.Entity))
                return;

            AddRecord(EntityType.Customer, eventMessage.Entity.Id, OperationType.Create);
        }

        /// <summary>
        /// Handle the customer updated event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task HandleEventAsync(EntityUpdatedEvent<Customer> eventMessage)
        {
            if (eventMessage.Entity == null || await _customerService.IsGuestAsync(eventMessage.Entity))
                return;

            var operationType = eventMessage.Entity.Deleted ? OperationType.Delete : OperationType.Update;
            AddRecord(EntityType.Customer, eventMessage.Entity.Id, operationType);
        }

        /// <summary>
        /// Handle the customer unsubscribed event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public Task HandleEventAsync(EmailUnsubscribedEvent eventMessage)
        {
            if (eventMessage.Subscription != null)
                AddRecord(EntityType.Subscription, null, OperationType.Delete, eventMessage.Subscription.Email);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle the newsletter subscription inserted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public Task HandleEventAsync(EntityInsertedEvent<NewsLetterSubscription> eventMessage)
        {
            if (eventMessage.Entity != null)
                AddRecord(EntityType.Subscription, eventMessage.Entity.Id, OperationType.Create);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle the newsletter subscription updated event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public Task HandleEventAsync(EntityUpdatedEvent<NewsLetterSubscription> eventMessage)
        {
            if (eventMessage.Entity != null)
                AddRecord(EntityType.Subscription, eventMessage.Entity.Id, OperationType.Update);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle the newsletter subscription deleted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public Task HandleEventAsync(EntityDeletedEvent<NewsLetterSubscription> eventMessage)
        {
            if (eventMessage.Entity != null)
                AddRecord(EntityType.Subscription, eventMessage.Entity.Id, OperationType.Delete, eventMessage.Entity.Email);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle the product inserted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public Task HandleEventAsync(EntityInsertedEvent<Product> eventMessage)
        {
            if (eventMessage.Entity != null)
                AddRecord(EntityType.Product, eventMessage.Entity.Id, OperationType.Create);
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle the product updated event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public Task HandleEventAsync(EntityUpdatedEvent<Product> eventMessage)
        {
            if (eventMessage.Entity != null)
            {
                var operationType = eventMessage.Entity.Deleted ? OperationType.Delete : OperationType.Update;
                AddRecord(EntityType.Product, eventMessage.Entity.Id, operationType);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle the product attribute mapping deleted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task HandleEventAsync(EntityDeletedEvent<ProductAttributeMapping> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            //update combinations related with deleted product attribute mapping
            var combinations = (await _productAttributeService.GetAllProductAttributeCombinationsAsync(eventMessage.Entity.ProductId))
                .WhereAwait(async combination => (await _productAttributeParser.ParseProductAttributeMappingsAsync(combination.AttributesXml))
                    .Any(productAttributeMapping => productAttributeMapping.Id == eventMessage.Entity.Id));
            await foreach (var combination in combinations)
            {
                AddRecord(EntityType.AttributeCombination, combination.Id, OperationType.Update);
            }
        }

        /// <summary>
        /// Handle the product attribute deleted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task HandleEventAsync(EntityDeletedEvent<ProductAttribute> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            //get associated product attribute mapping objects
            var productAttributeMappings = (await _productService.GetProductsByProductAtributeIdAsync(eventMessage.Entity.Id))
                .SelectManyAwait(async product => (await _productAttributeService.GetProductAttributeMappingsByProductIdAsync(product.Id))
                .Where(attribute => attribute.ProductId > 0 && attribute.ProductAttributeId == eventMessage.Entity.Id));
            await foreach (var productAttributeMapping in productAttributeMappings)
            {
                //update combinations related with deleted product attribute
                var combinations = (await _productAttributeService.GetAllProductAttributeCombinationsAsync(productAttributeMapping.ProductId))
                    .WhereAwait(async combination => (await _productAttributeParser.ParseProductAttributeMappingsAsync(combination.AttributesXml))
                        .Any(mapping => mapping.Id == productAttributeMapping.Id));
                await foreach (var combination in combinations)
                {
                    AddRecord(EntityType.AttributeCombination, combination.Id, OperationType.Update);
                }
            }
        }

        /// <summary>
        /// Handle the product attribute value updated event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task HandleEventAsync(EntityUpdatedEvent<ProductAttributeValue> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            //get associated product attribute mapping object
            var productAttributeMapping = await _productAttributeService.GetProductAttributeMappingByIdAsync(eventMessage.Entity.ProductAttributeMappingId);
            if (productAttributeMapping == null)
                return;

            //update combinations related with updated product attribute value
            var combinations = (await _productAttributeService.GetAllProductAttributeCombinationsAsync(productAttributeMapping.ProductId))
                .WhereAwait(async combination => (await _productAttributeParser.ParseProductAttributeValuesAsync(combination.AttributesXml, productAttributeMapping.Id))
                    .Any(value => value.Id == eventMessage.Entity.Id));
            await foreach (var combination in combinations)
            {
                AddRecord(EntityType.AttributeCombination, combination.Id, OperationType.Update);
            }
        }

        /// <summary>
        /// Handle the product attribute value deleted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task HandleEventAsync(EntityDeletedEvent<ProductAttributeValue> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            //get associated product attribute mapping object
            var productAttributeMapping = await _productAttributeService.GetProductAttributeMappingByIdAsync(eventMessage.Entity.ProductAttributeMappingId);
            if (productAttributeMapping == null)
                return;

            //update combinations related with deleted product attribute value
            var combinations = (await _productAttributeService.GetAllProductAttributeCombinationsAsync(productAttributeMapping.ProductId))
                .WhereAwait(async combination => (await _productAttributeParser.ParseProductAttributeValuesAsync(combination.AttributesXml, productAttributeMapping.Id))
                    .Any(value => value.Id == eventMessage.Entity.Id));
            await foreach (var combination in combinations)
            {
                AddRecord(EntityType.AttributeCombination, combination.Id, OperationType.Update);
            }
        }

        /// <summary>
        /// Handle the product attribute combination inserted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public Task HandleEventAsync(EntityInsertedEvent<ProductAttributeCombination> eventMessage)
        {
            if (eventMessage.Entity != null)
                AddRecord(EntityType.AttributeCombination, eventMessage.Entity.Id, OperationType.Create);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle the product attribute combination updated event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public Task HandleEventAsync(EntityUpdatedEvent<ProductAttributeCombination> eventMessage)
        {
            if (eventMessage.Entity != null)
                AddRecord(EntityType.AttributeCombination, eventMessage.Entity.Id, OperationType.Update);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle the product attribute combination deleted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public Task HandleEventAsync(EntityDeletedEvent<ProductAttributeCombination> eventMessage)
        {
            if (eventMessage.Entity != null)
                AddRecord(EntityType.AttributeCombination, eventMessage.Entity.Id, OperationType.Delete, productId: eventMessage.Entity.ProductId);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle the order inserted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public Task HandleEventAsync(EntityInsertedEvent<Order> eventMessage)
        {
            if (eventMessage.Entity != null)
                AddRecord(EntityType.Order, eventMessage.Entity.Id, OperationType.Create);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handle the order inserted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public Task HandleEventAsync(EntityUpdatedEvent<Order> eventMessage)
        {
            if (eventMessage.Entity != null)
            {
                var operationType = eventMessage.Entity.Deleted ? OperationType.Delete : OperationType.Update;
                AddRecord(EntityType.Order, eventMessage.Entity.Id, operationType);
            }

            return Task.CompletedTask;
        }

        #endregion
    }
}