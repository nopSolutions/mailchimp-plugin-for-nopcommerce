using System.Linq;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Core.Events;
using Nop.Plugin.Misc.MailChimp.Domain;
using Nop.Services.Catalog;
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

        private readonly IProductAttributeParser _productAttributeParser;
        private readonly IProductAttributeService _productAttributeService;
        private readonly IProductService _productService;
        private readonly ISynchronizationRecordService _synchronizationRecordService;

        #endregion

        #region Ctor

        public EventConsumer(IProductAttributeParser productAttributeParser,
            IProductAttributeService productAttributeService,
            IProductService productService,
            ISynchronizationRecordService synchronizationRecordService)
        {
            this._productAttributeParser = productAttributeParser;
            this._productAttributeService = productAttributeService;
            this._productService = productService;
            this._synchronizationRecordService = synchronizationRecordService;
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
            _synchronizationRecordService.CreateOrUpdateRecord(entityType, id ?? 0, operationType, email, productId ?? 0);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Handle the store inserted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityInsertedEvent<Store> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            AddRecord(EntityType.Store, eventMessage.Entity.Id, OperationType.Create);
        }

        /// <summary>
        /// Handle the store updated event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityUpdatedEvent<Store> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            AddRecord(EntityType.Store, eventMessage.Entity.Id, OperationType.Update);
        }

        /// <summary>
        /// Handle the store deleted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityDeletedEvent<Store> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            AddRecord(EntityType.Store, eventMessage.Entity.Id, OperationType.Delete);
        }

        /// <summary>
        /// Handle the customer registered event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(CustomerRegisteredEvent eventMessage)
        {
            if (eventMessage.Customer == null)
                return;

            AddRecord(EntityType.Customer, eventMessage.Customer.Id, OperationType.Create);
        }

        /// <summary>
        /// Handle the customer inserted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityInsertedEvent<Customer> eventMessage)
        {
            if (eventMessage.Entity?.IsGuest() ?? true)
                return;

            AddRecord(EntityType.Customer, eventMessage.Entity.Id, OperationType.Create);
        }

        /// <summary>
        /// Handle the customer updated event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityUpdatedEvent<Customer> eventMessage)
        {
            if (eventMessage.Entity?.IsGuest() ?? true)
                return;

            var operationType = eventMessage.Entity.Deleted ? OperationType.Delete : OperationType.Update;
            AddRecord(EntityType.Customer, eventMessage.Entity.Id, operationType);
        }

        /// <summary>
        /// Handle the customer unsubscribed event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EmailUnsubscribedEvent eventMessage)
        {
            if (eventMessage.Subscription == null)
                return;

            AddRecord(EntityType.Subscription, null, OperationType.Delete, eventMessage.Subscription.Email);
        }

        /// <summary>
        /// Handle the newsletter subscription inserted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityInsertedEvent<NewsLetterSubscription> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            AddRecord(EntityType.Subscription, eventMessage.Entity.Id, OperationType.Create);
        }

        /// <summary>
        /// Handle the newsletter subscription updated event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityUpdatedEvent<NewsLetterSubscription> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            AddRecord(EntityType.Subscription, eventMessage.Entity.Id, OperationType.Update);
        }

        /// <summary>
        /// Handle the newsletter subscription deleted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityDeletedEvent<NewsLetterSubscription> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            AddRecord(EntityType.Subscription, eventMessage.Entity.Id, OperationType.Delete, eventMessage.Entity.Email);
        }

        /// <summary>
        /// Handle the product inserted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityInsertedEvent<Product> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            AddRecord(EntityType.Product, eventMessage.Entity.Id, OperationType.Create);
        }

        /// <summary>
        /// Handle the product updated event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityUpdatedEvent<Product> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            var operationType = eventMessage.Entity.Deleted ? OperationType.Delete : OperationType.Update;
            AddRecord(EntityType.Product, eventMessage.Entity.Id, operationType);
        }

        /// <summary>
        /// Handle the product attribute mapping deleted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityDeletedEvent<ProductAttributeMapping> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            //update combinations related with deleted product attribute mapping
            var combinations = _productAttributeService.GetAllProductAttributeCombinations(eventMessage.Entity.ProductId)
                .Where(combination => _productAttributeParser.ParseProductAttributeMappings(combination.AttributesXml)
                    .Any(productAttributeMapping => productAttributeMapping.Id == eventMessage.Entity.Id));
            foreach (var combination in combinations)
            {
                AddRecord(EntityType.AttributeCombination, combination.Id, OperationType.Update);
            }
        }

        /// <summary>
        /// Handle the product attribute deleted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityDeletedEvent<ProductAttribute> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            //get associated product attribute mapping objects
            var productAttributeMappings = _productService.GetProductsByProductAtributeId(eventMessage.Entity.Id)
                .SelectMany(product => _productAttributeService.GetProductAttributeMappingsByProductId(product.Id)
                .Where(attribute => attribute.Product != null && attribute.ProductAttributeId == eventMessage.Entity.Id));
            foreach (var productAttributeMapping in productAttributeMappings)
            {
                //update combinations related with deleted product attribute
                var combinations = _productAttributeService.GetAllProductAttributeCombinations(productAttributeMapping.ProductId)
                    .Where(combination => _productAttributeParser.ParseProductAttributeMappings(combination.AttributesXml)
                        .Any(mapping => mapping.Id == productAttributeMapping.Id));
                foreach (var combination in combinations)
                {
                    AddRecord(EntityType.AttributeCombination, combination.Id, OperationType.Update);
                }
            }
        }

        /// <summary>
        /// Handle the product attribute value updated event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityUpdatedEvent<ProductAttributeValue> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            //get associated product attribute mapping object
            var productAttributeMapping = _productAttributeService.GetProductAttributeMappingById(eventMessage.Entity.ProductAttributeMappingId);
            if (productAttributeMapping == null)
                return;

            //update combinations related with updated product attribute value
            var combinations = _productAttributeService.GetAllProductAttributeCombinations(productAttributeMapping.ProductId)
                .Where(combination => _productAttributeParser.ParseProductAttributeValues(combination.AttributesXml, productAttributeMapping.Id)
                    .Any(value => value.Id == eventMessage.Entity.Id));
            foreach (var combination in combinations)
            {
                AddRecord(EntityType.AttributeCombination, combination.Id, OperationType.Update);
            }
        }

        /// <summary>
        /// Handle the product attribute value deleted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityDeletedEvent<ProductAttributeValue> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            //get associated product attribute mapping object
            var productAttributeMapping = _productAttributeService.GetProductAttributeMappingById(eventMessage.Entity.ProductAttributeMappingId);
            if (productAttributeMapping == null)
                return;

            //update combinations related with deleted product attribute value
            var combinations = _productAttributeService.GetAllProductAttributeCombinations(productAttributeMapping.ProductId)
                .Where(combination => _productAttributeParser.ParseProductAttributeValues(combination.AttributesXml, productAttributeMapping.Id)
                    .Any(value => value.Id == eventMessage.Entity.Id));
            foreach (var combination in combinations)
            {
                AddRecord(EntityType.AttributeCombination, combination.Id, OperationType.Update);
            }
        }

        /// <summary>
        /// Handle the product attribute combination inserted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityInsertedEvent<ProductAttributeCombination> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            AddRecord(EntityType.AttributeCombination, eventMessage.Entity.Id, OperationType.Create);
        }

        /// <summary>
        /// Handle the product attribute combination updated event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityUpdatedEvent<ProductAttributeCombination> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            AddRecord(EntityType.AttributeCombination, eventMessage.Entity.Id, OperationType.Update);
        }

        /// <summary>
        /// Handle the product attribute combination deleted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityDeletedEvent<ProductAttributeCombination> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            AddRecord(EntityType.AttributeCombination, eventMessage.Entity.Id, OperationType.Delete, productId: eventMessage.Entity.ProductId);
        }

        /// <summary>
        /// Handle the order inserted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityInsertedEvent<Order> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            AddRecord(EntityType.Order, eventMessage.Entity.Id, OperationType.Create);
        }

        /// <summary>
        /// Handle the order inserted event
        /// </summary>
        /// <param name="eventMessage">Event message</param>
        public void HandleEvent(EntityUpdatedEvent<Order> eventMessage)
        {
            if (eventMessage.Entity == null)
                return;

            var operationType = eventMessage.Entity.Deleted ? OperationType.Delete : OperationType.Update;
            AddRecord(EntityType.Order, eventMessage.Entity.Id, operationType);
        }

        #endregion
    }
}