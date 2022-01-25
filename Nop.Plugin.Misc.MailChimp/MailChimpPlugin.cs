using Nop.Core;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Plugin.Misc.MailChimp.Services;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using System;
using System.Collections.Generic;
using Task = System.Threading.Tasks.Task;

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

        #endregion

        #region Ctor

        public MailChimpPlugin(ILocalizationService localizationService,
            IScheduleTaskService scheduleTaskService,
            ISettingService settingService,
            IWebHelper webHelper,
            MailChimpManager mailChimpManager)
        {
            _localizationService = localizationService;
            _scheduleTaskService = scheduleTaskService;
            _settingService = settingService;
            _webHelper = webHelper;
            _mailChimpManager = mailChimpManager;
            
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
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task InstallAsync()
        {
            //settings
            await _settingService.SaveSettingAsync(new MailChimpSettings
            {
                ListId = Guid.Empty.ToString(),
                StoreIdMask = MailChimpDefaults.DefaultStoreIdMask,
                BatchOperationNumber = MailChimpDefaults.DefaultBatchOperationNumber
            });

            //synchronization task
            if (await _scheduleTaskService.GetTaskByTypeAsync(MailChimpDefaults.SynchronizationTask) == null)
            {
                await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
                {
                    Type = MailChimpDefaults.SynchronizationTask,
                    Name = MailChimpDefaults.SynchronizationTaskName,
                    Seconds = MailChimpDefaults.DefaultSynchronizationPeriod * 60 * 60
                });
            }

            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
            {
                ["Plugins.Misc.MailChimp.Fields.AccountInfo"] = "Account information",
                ["Plugins.Misc.MailChimp.Fields.AccountInfo.Hint"] = "Display MailChimp account information.",
                ["Plugins.Misc.MailChimp.Fields.ApiKey"] = "API key",
                ["Plugins.Misc.MailChimp.Fields.ApiKey.Hint"] = "Enter your MailChimp account API key.",
                ["Plugins.Misc.MailChimp.Fields.AutoSynchronization"] = "Use auto synchronization",
                ["Plugins.Misc.MailChimp.Fields.AutoSynchronization.Hint"] = "Determine whether to use auto synchronization.",
                ["Plugins.Misc.MailChimp.Fields.AutoSynchronization.Restart"] = "Auto synchronization parameters has been changed] =please restart the application",
                ["Plugins.Misc.MailChimp.Fields.List"] = "List",
                ["Plugins.Misc.MailChimp.Fields.List.Hint"] = "Choose list of users for the synchronization.",
                ["Plugins.Misc.MailChimp.Fields.List.NotExist"] = "There are no lists",
                ["Plugins.Misc.MailChimp.Fields.PassEcommerceData"] = "Pass E-Commerce data",
                ["Plugins.Misc.MailChimp.Fields.PassEcommerceData.Hint"] = "Determine whether to pass E-Commerce data (customers] =products] =orders] =etc).",
                ["Plugins.Misc.MailChimp.Fields.SynchronizationPeriod"] = "Synchronization period",
                ["Plugins.Misc.MailChimp.Fields.SynchronizationPeriod.Hint"] = "Specify the synchronization period in hours.",
                ["Plugins.Misc.MailChimp.ManualSynchronization"] = "Synchronize",
                ["Plugins.Misc.MailChimp.ManualSynchronization.Hint"] = "Manually synchronize",
                ["Plugins.Misc.MailChimp.Synchronization.Error"] = "An error occurred during synchronization with MailChimp",
                ["Plugins.Misc.MailChimp.Synchronization.Started"] = "Synchronization is in progress",
                ["Plugins.Misc.MailChimp.Webhook.Warning"] = "Webhook was not created (you'll not be able to get unsubscribed users)"
            });
            await base.InstallAsync();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task UninstallAsync()
        {
            //webhooks
            await _mailChimpManager.DeleteBatchWebhookAsync();
            await _mailChimpManager.DeleteWebhooksAsync();

            //synchronization task
            var task = await _scheduleTaskService.GetTaskByTypeAsync(MailChimpDefaults.SynchronizationTask);
            if (task != null)
                await _scheduleTaskService.DeleteTaskAsync(task);

            //settings
            await _settingService.DeleteSettingAsync<MailChimpSettings>();

            //locales
            await _localizationService.DeleteLocaleResourcesAsync("Plugins.Misc.MailChimp");

            await base.UninstallAsync();
        }

        #endregion
    }
}