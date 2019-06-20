using System;
using Nop.Core;
using Nop.Core.Domain.Tasks;
using Nop.Plugin.Misc.MailChimp.Data;
using Nop.Plugin.Misc.MailChimp.Services;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.Tasks;

namespace Nop.Plugin.Misc.MailChimp
{
    /// <summary>
    /// Represents the MailChimp plugin 
    /// </summary>
    public class MailChimpPlugin : BasePlugin, IMiscPlugin
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly IScheduleTaskService _scheduleTaskService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly MailChimpManager _mailChimpManager;
        private readonly MailChimpObjectContext _mailChimpObjectContext;

        #endregion

        #region Ctor

        public MailChimpPlugin(ILocalizationService localizationService,
            IScheduleTaskService scheduleTaskService,
            ISettingService settingService,
            IWebHelper webHelper,
            MailChimpManager mailChimpManager,
            MailChimpObjectContext mailChimpObjectContext)
        {
            _localizationService = localizationService;
            _scheduleTaskService = scheduleTaskService;
            _settingService = settingService;
            _webHelper = webHelper;
            _mailChimpManager = mailChimpManager;
            _mailChimpObjectContext = mailChimpObjectContext;
            
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
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AccountInfo", "Account information");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AccountInfo.Hint", "Display MailChimp account information.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.ApiKey", "API key");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.ApiKey.Hint", "Enter your MailChimp account API key.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSynchronization", "Use auto synchronization");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSynchronization.Hint", "Determine whether to use auto synchronization.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSynchronization.Restart", "Auto synchronization parameters has been changed, please restart the application");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.List", "List");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.List.Hint", "Choose list of users for the synchronization.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.List.NotExist", "There are no lists");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.PassEcommerceData", "Pass E-Commerce data");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.PassEcommerceData.Hint", "Determine whether to pass E-Commerce data (customers, products, orders, etc).");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.SynchronizationPeriod", "Synchronization period");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.SynchronizationPeriod.Hint", "Specify the synchronization period in hours.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.ManualSynchronization", "Synchronize");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.ManualSynchronization.Hint", "Manually synchronize");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Synchronization.Error", "An error occurred during synchronization with MailChimp");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Synchronization.Started", "Synchronization is in progress");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Webhook.Warning", "Webhook was not created (you'll not be able to get unsubscribed users)");

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
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AccountInfo");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AccountInfo.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.ApiKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.ApiKey.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSynchronization");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSynchronization.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSynchronization.Restart");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.List");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.List.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.List.NotExist");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.PassEcommerceData");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.PassEcommerceData.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.SynchronizationPeriod");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.SynchronizationPeriod.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.MailChimp.ManualSynchronization");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.MailChimp.ManualSynchronization.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Synchronization.Error");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Synchronization.Started");
            _localizationService.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Webhook.Warning");

            base.Uninstall();
        }

        #endregion
    }
}