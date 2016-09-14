using System.Web.Routing;
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
        private readonly MailChimpManager _mailChimpManager;
        private readonly MailChimpObjectContext _mailChimpObjectContext;

        #endregion

        #region Ctor

        public MailChimpPlugin(IScheduleTaskService scheduleTaskService,
            ISettingService settingService,
            IStoreService storeService,
            MailChimpManager mailChimpManager,
            MailChimpObjectContext mailChimpObjectContext)
        {
            this._scheduleTaskService = scheduleTaskService;
            this._settingService = settingService;
            this._storeService = storeService;
            this._mailChimpManager = mailChimpManager;
            this._mailChimpObjectContext = mailChimpObjectContext;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Synchronize nopCommerce data with MailChimp
        /// </summary>
        public void Synchronize()
        {
            //we try to get a result, otherwise scheduled task does not start manually
            var result = System.Threading.Tasks.Task.Run(() => _mailChimpManager.Synchronize()).Result;
        }

        /// <summary>
        /// Gets a route for plugin configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "MailChimp";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Misc.MailChimp.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            _settingService.SaveSetting(new MailChimpSettings());

            //install synchronization task
            if (_scheduleTaskService.GetTaskByType("Nop.Plugin.Misc.MailChimp.Services.MailChimpSynchronizationTask, Nop.Plugin.Misc.MailChimp") == null)
            {
                _scheduleTaskService.InsertTask(new ScheduleTask
                {
                    Name = "MailChimp synchronization",
                    Seconds = 21600,
                    Type = "Nop.Plugin.Misc.MailChimp.Services.MailChimpSynchronizationTask, Nop.Plugin.Misc.MailChimp",
                });
            }

            //database objects
            _mailChimpObjectContext.Install();

            //data for the first synchronization
            _mailChimpManager.CreateInitiateData();

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.AccountInfo", "Account information");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.AutoSyncRestart", "Task parameters has been changed, please restart the application");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.ManualSync", "Synchronize");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.ManualSync.Hint", "Manual synchronization");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.ApiKey", "API key");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.ApiKey.Hint", "Input your MailChimp account API key.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSync", "Auto synchronization");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSync.Hint", "Use auto synchronization task.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSyncEachMinutes", "Period (minutes)");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSyncEachMinutes.Hint", "Input auto synchronization task period (minutes).");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.List", "List");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.List.Hint", "Choose list of contacts for the synchronization.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.UseEcommerceApi", "Use MailChimp for E-Commerce");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.Fields.UseEcommerceApi.Hint", "Check for using MailChimp for E-Commerce.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.SynchronizationError", "Error on synchronization start");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.SynchronizationStart", "Synchronization is in process");
            this.AddOrUpdatePluginLocaleResource("Plugins.Misc.MailChimp.WebhookError", "Webhook was not created (you'll not be able to get users who unsubscribed from MailChimp)");

            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //delete webhooks
            foreach (var store in _storeService.GetAllStores())
            {
                var currentSettings = _settingService.LoadSetting<MailChimpSettings>(store.Id);
                if (!string.IsNullOrEmpty(currentSettings.ListId) && !string.IsNullOrEmpty(currentSettings.WebhookId))
                    _mailChimpManager.DeleteWebhook(currentSettings.ListId, currentSettings.WebhookId);
            }

            //settings
            _settingService.DeleteSetting<MailChimpSettings>();

            //remove scheduled task
            var task = _scheduleTaskService.GetTaskByType("Nop.Plugin.Misc.MailChimp.Services.MailChimpSynchronizationTask, Nop.Plugin.Misc.MailChimp");
            if (task != null)
                _scheduleTaskService.DeleteTask(task);

            //database objects
            _mailChimpObjectContext.Uninstall();

            //locales
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.AccountInfo");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.AutoSyncRestart");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.ManualSync");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.ManualSync.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.ApiKey");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.ApiKey.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSync");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSync.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSyncEachMinutes");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.AutoSyncEachMinutes.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.List");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.List.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.UseEcommerceApi");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.Fields.UseEcommerceApi.Hint");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.SynchronizationError");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.SynchronizationStart");
            this.DeletePluginLocaleResource("Plugins.Misc.MailChimp.WebhookError");

            base.Uninstall();
        }

        #endregion
    }
}
