using System.Linq;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Core.Events;
using Nop.Core.Plugins;
using Nop.Plugin.Misc.MailChimp.Domain;
using Nop.Plugin.Misc.MailChimp.Services;
using Nop.Services.Catalog;
using Nop.Services.Events;

namespace Nop.Plugin.Misc.MailChimp.Infrastructure.Cache
{
    public class MailChimpEventConsumer :
        //stores
        IConsumer<EntityInserted<Store>>,
        IConsumer<EntityUpdated<Store>>,
        IConsumer<EntityDeleted<Store>>,
        //customers
        IConsumer<CustomerRegisteredEvent>,
        IConsumer<EntityInserted<Customer>>,
        IConsumer<EntityUpdated<Customer>>,
        //subscriptions
        IConsumer<EmailUnsubscribedEvent>,
        IConsumer<EntityInserted<NewsLetterSubscription>>,
        IConsumer<EntityUpdated<NewsLetterSubscription>>,
        IConsumer<EntityDeleted<NewsLetterSubscription>>,
        //products
        IConsumer<EntityInserted<Product>>,
        IConsumer<EntityUpdated<Product>>,
        //product attribute
        IConsumer<EntityInserted<ProductAttributeMapping>>,
        IConsumer<EntityUpdated<ProductAttributeMapping>>,
        IConsumer<EntityDeleted<ProductAttributeMapping>>,
        IConsumer<EntityDeleted<ProductAttribute>>,
        //attribute values
        IConsumer<EntityInserted<ProductAttributeValue>>,
        IConsumer<EntityUpdated<ProductAttributeValue>>,
        IConsumer<EntityDeleted<ProductAttributeValue>>,
        //attribute combinations
        IConsumer<EntityInserted<ProductAttributeCombination>>,
        IConsumer<EntityUpdated<ProductAttributeCombination>>,
        IConsumer<EntityDeleted<ProductAttributeCombination>>,
        //orders
        IConsumer<EntityInserted<Order>>,
        IConsumer<EntityUpdated<Order>>
    {
        #region Fields

        private readonly IPluginFinder _pluginFinder;
        private readonly IProductAttributeService _productAttributeService;
        private readonly IProductService _productService;
        private readonly ISynchronizationRecordService _synchronizationRecordService;

        #endregion

        #region Ctor

        public MailChimpEventConsumer(IPluginFinder pluginFinder,
            IProductAttributeService productAttributeService,
            IProductService productService,
            ISynchronizationRecordService synchronizationRecordService)
        {
            this._pluginFinder = pluginFinder;
            this._productAttributeService = productAttributeService;
            this._productService = productService;
            this._synchronizationRecordService = synchronizationRecordService;
        }

        #endregion

        #region Properties

        protected bool PluginIsActive
        {
            get
            {
                var pluginDescriptor = _pluginFinder.GetPluginDescriptorBySystemName("Misc.MailChimp");
                if (pluginDescriptor == null)
                    return false;

                var plugin = pluginDescriptor.Instance() as MailChimpPlugin;

                return plugin != null;
            }
        }

        #endregion

        #region Methods

        #region Store

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="insertedStore">Inserted store</param>
        public void HandleEvent(EntityInserted<Store> insertedStore)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Store, insertedStore.Entity.Id, ActionType.Create);
        }

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="updatedStore">Updated store</param>
        public void HandleEvent(EntityUpdated<Store> updatedStore)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Store, updatedStore.Entity.Id, ActionType.Update);
        }

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="deletedStore">Deleted store</param>
        public void HandleEvent(EntityDeleted<Store> deletedStore)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Store, deletedStore.Entity.Id, ActionType.Delete);
        }

        #endregion

        #region Customer

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="registeredCustomer">Registered customer</param>
        public void HandleEvent(CustomerRegisteredEvent registeredCustomer)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Customer, registeredCustomer.Customer.Id, ActionType.Create);
        }

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="insertedCustomer">Inserted customer</param>
        public void HandleEvent(EntityInserted<Customer> insertedCustomer)
        {
            if (PluginIsActive && !insertedCustomer.Entity.IsGuest())
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Customer, insertedCustomer.Entity.Id, ActionType.Create);
        }

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="updatedCustomer">Updated customer</param>
        public void HandleEvent(EntityUpdated<Customer> updatedCustomer)
        {
            if (PluginIsActive && !updatedCustomer.Entity.IsGuest())
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Customer, updatedCustomer.Entity.Id,
                    updatedCustomer.Entity.Deleted ? ActionType.Delete : ActionType.Update);
        }

        #endregion

        #region Subscription

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="unsubscription">Unsubscription</param>
        public void HandleEvent(EmailUnsubscribedEvent unsubscription)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Subscription, 0, ActionType.Delete, unsubscription.Subscription.Email);
        }

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="insertedSubscription">Inserted subscription</param>
        public void HandleEvent(EntityInserted<NewsLetterSubscription> insertedSubscription)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Subscription, insertedSubscription.Entity.Id, ActionType.Create);
        }

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="updatedSubscription">Updated subscription</param>
        public void HandleEvent(EntityUpdated<NewsLetterSubscription> updatedSubscription)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Subscription, updatedSubscription.Entity.Id, ActionType.Update);
        }

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="deletedSubscription">Deleted subscription</param>
        public void HandleEvent(EntityDeleted<NewsLetterSubscription> deletedSubscription)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Subscription, deletedSubscription.Entity.Id, ActionType.Delete,
                    deletedSubscription.Entity.Email);
        }

        #endregion

        #region Products

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="insertedProduct">Inserted product</param>
        public void HandleEvent(EntityInserted<Product> insertedProduct)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Product, insertedProduct.Entity.Id, ActionType.Create);
        }

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="updatedProduct">Updated product</param>
        public void HandleEvent(EntityUpdated<Product> updatedProduct)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Product, updatedProduct.Entity.Id,
                    updatedProduct.Entity.Deleted ? ActionType.Delete : ActionType.Update);
        }

        #endregion

        #region Product attributes

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="insertedProductAttribute">Inserted product attribute</param>
        public void HandleEvent(EntityInserted<ProductAttributeMapping> insertedProductAttribute)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.ProductAttribute, insertedProductAttribute.Entity.Id, ActionType.Create);
        }

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="updatedProductAttribute">Updated product attribute</param>
        public void HandleEvent(EntityUpdated<ProductAttributeMapping> updatedProductAttribute)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.ProductAttribute, updatedProductAttribute.Entity.Id, ActionType.Update);
        }

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="deletedProductAttribute">Deleted product attribute</param>
        public void HandleEvent(EntityDeleted<ProductAttributeMapping> deletedProductAttribute)
        {
            if (!PluginIsActive)
                return;

            //delete attribute
            _synchronizationRecordService.CreateOrUpdateRecord(EntityType.ProductAttribute, deletedProductAttribute.Entity.Id, ActionType.Delete,
                productId: deletedProductAttribute.Entity.ProductId);

            //and child values
            foreach (var value in deletedProductAttribute.Entity.ProductAttributeValues)
            {
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.AttributeValue, value.Id, ActionType.Delete,
                    productId: deletedProductAttribute.Entity.ProductId);
            }
        }

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="deletedAttribute">Deleted attribute</param>
        public void HandleEvent(EntityDeleted<ProductAttribute> deletedAttribute)
        {
            if (!PluginIsActive)
                return;

            //get associated product attribute mapping objects
            var productAttributeMappings = _productService.GetProductsByProductAtributeId(deletedAttribute.Entity.Id)
                .SelectMany(product => _productAttributeService.GetProductAttributeMappingsByProductId(product.Id)
                .Where(attribute => attribute.ProductAttributeId == deletedAttribute.Entity.Id));
            foreach (var productAttributeMapping in productAttributeMappings)
            {
                //delete attribute
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.ProductAttribute, productAttributeMapping.Id, ActionType.Delete,
                    productId: productAttributeMapping.ProductId);

                //and child values
                foreach (var value in productAttributeMapping.ProductAttributeValues)
                {
                    _synchronizationRecordService.CreateOrUpdateRecord(EntityType.AttributeValue, value.Id, ActionType.Delete,
                        productId: productAttributeMapping.ProductId);
                }
            }
        }

        #endregion

        #region Attribute values

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="insertedValue">Inserted attribute value</param>
        public void HandleEvent(EntityInserted<ProductAttributeValue> insertedValue)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.AttributeValue, insertedValue.Entity.Id, ActionType.Create);
        }

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="updatedValue">Updated attribute value</param>
        public void HandleEvent(EntityUpdated<ProductAttributeValue> updatedValue)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.AttributeValue, updatedValue.Entity.Id, ActionType.Update);
        }

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="deletedValue">Deleted attribute value</param>
        public void HandleEvent(EntityDeleted<ProductAttributeValue> deletedValue)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.AttributeValue, deletedValue.Entity.Id, ActionType.Delete,
                    productId: deletedValue.Entity.ProductAttributeMapping != null ? deletedValue.Entity.ProductAttributeMapping.ProductId : 0);
        }

        #endregion

        #region Attribute combinations

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="insertedCombination">Inserted attribute combination</param>
        public void HandleEvent(EntityInserted<ProductAttributeCombination> insertedCombination)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.AttributeCombination, insertedCombination.Entity.Id, ActionType.Create);
        }

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="updatedCombination">Updated attribute combination</param>
        public void HandleEvent(EntityUpdated<ProductAttributeCombination> updatedCombination)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.AttributeCombination, updatedCombination.Entity.Id, ActionType.Update);
        }

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="deletedCombination">Deleted attribute combination</param>
        public void HandleEvent(EntityDeleted<ProductAttributeCombination> deletedCombination)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.AttributeCombination, deletedCombination.Entity.Id, ActionType.Delete,
                    productId: deletedCombination.Entity.ProductId);
        }

        #endregion

        #region Orders

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="insertedOrder">Inserted order</param>
        public void HandleEvent(EntityInserted<Order> insertedOrder)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Order, insertedOrder.Entity.Id, ActionType.Create);
        }

        /// <summary>
        /// Handles the event.
        /// </summary>
        /// <param name="updatedOrder">Updated order</param>
        public void HandleEvent(EntityUpdated<Order> updatedOrder)
        {
            if (PluginIsActive)
                _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Order, updatedOrder.Entity.Id,
                    updatedOrder.Entity.Deleted ? ActionType.Delete : ActionType.Update);
        }

        #endregion

        #endregion
    }
}