using System;
using Nop.Core;
using Nop.Core.Domain.Tasks;
using Nop.Core.Plugins;
using Nop.Plugin.Misc.MailChimp.Data;
using Nop.Plugin.Misc.MailChimp.Services;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Stores;
using Nop.Services.Tasks;

namespace Nop.Plugin.Misc.MailChimp
{
    /// <summary>
    /// Represents the MailChimp plugin 
    /// </summary>
    public class MailChimpPlugin : BasePlugin, IMiscPlugin
    {
        #region Fields

        private readonly IScheduleTaskService _scheduleTaskService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;
        private readonly IWebHelper _webHelper;
        private readonly MailChimpManager _mailChimpManager;
        private readonly MailChimpObjectContext _mailChimpObjectContext;

        #endregion

        #region Ctor

        public MailChimpPlugin(IScheduleTaskService scheduleTaskService,
            ISettingService settingService,
            IStoreService storeService,
            IWebHelper webHelper,
            MailChimpManager mailChimpManager,
            MailChimpObjectContext mailChimpObjectContext)
        {
            this._scheduleTaskService = scheduleTaskService;
            this._settingService = settingService;
            this._storeService = storeService;
            this._webHelper = webHelper;
            this._mailChimpManager = mailChimpManager;
            this._mailChimpObjectContext = mailChimpObjectContext;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/MailChimp/Configure";
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            _settingService.SaveSetting(new MailChimpSettings
            {
                ListId = Guid.Empty.ToString(),
                StoreIdMask = MailChimpDefaults.DefaultStoreIdMask,
                BatchOperationNumber = MailChimpDefaults.DefaultBatchOperationNumber
            });

            //synchronization task
            if (_scheduleTaskService.GetTaskByType(MailChimpDefaults.SynchronizationTask) == null)
            {
                _scheduleTaskService.InsertTask(new ScheduleTask
                {
                    Type = MailChimpDefaults.SynchronizationTask,
                    Name = MailChimpDefaults.SynchronizationTaskName,
                    Seconds = MailChimpDefaults.DefaultSynchronizationPeriod * 60 * 60
                });
            }

            //database objects
            _mailChimpObjectContext.Install();

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AccountInfo", "Account information");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AccountInfo.Hint", "Display MailChimp account information.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.ApiKey", "API key");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.ApiKey.Hint", "Enter your MailChimp account API key.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSynchronization", "Use auto synchronization");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSynchronization.Hint", "Determine whether to use auto synchronization.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSynchronization.Restart", "Auto synchronization parameters has been changed, please restart the application");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.List", "List");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.List.Hint", "Choose list of users for the synchronization.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.List.NotExist", "There are no lists");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.PassEcommerceData", "Pass E-Commerce data");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.PassEcommerceData.Hint", "Determine whether to pass E-Commerce data (customers, products, orders, etc).");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.SynchronizationPeriod", "Synchronization period");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.SynchronizationPeriod.Hint", "Specify the synchronization period in hours.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.ManualSynchronization", "Synchronize");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.ManualSynchronization.Hint", "Manually synchronize");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Synchronization.Error", "An error occurred during synchronization with MailChimp");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Synchronization.Started", "Synchronization is in progress");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Webhook.Warning", "Webhook was not created (you'll not be able to get unsubscribed users)");

            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //webhooks
            _mailChimpManager.DeleteBatchWebhook().Wait();
            _mailChimpManager.DeleteWebhooks().Wait();

            //database objects
            _mailChimpObjectContext.Uninstall();

            //synchronization task
            var task = _scheduleTaskService.GetTaskByType(MailChimpDefaults.SynchronizationTask);
            if (task != null)
                _scheduleTaskService.DeleteTask(task);

            //settings
            _settingService.DeleteSetting<MailChimpSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AccountInfo");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AccountInfo.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.ApiKey");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.ApiKey.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSynchronization");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSynchronization.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSynchronization.Restart");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.List");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.List.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.List.NotExist");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.PassEcommerceData");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.PassEcommerceData.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.SynchronizationPeriod");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.SynchronizationPeriod.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.ManualSynchronization");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.ManualSynchronization.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Synchronization.Error");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Synchronization.Started");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Webhook.Warning");

            base.Uninstall();
        }

        #endregion
    }
}