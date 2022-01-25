using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Plugin.Misc.MailChimp.Domain;
using Nop.Plugin.Misc.MailChimp.Models;
using Nop.Plugin.Misc.MailChimp.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.ScheduleTasks;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc;
using Nop.Web.Framework.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nop.Plugin.Misc.MailChimp.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class MailChimpController : BasePluginController
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly IScheduleTaskService _scheduleTaskService;
        private readonly ISettingService _settingService;
        private readonly IStaticCacheManager _staticCacheManager;
        private readonly IStoreContext _storeContext;
        private readonly IStoreService _storeService;
        private readonly ISynchronizationRecordService _synchronizationRecordService;
        private readonly MailChimpManager _mailChimpManager;

        #endregion

        #region Ctor

        public MailChimpController(
            ILocalizationService localizationService,
            INotificationService notificationService,
            IScheduleTaskService scheduleTaskService,
            ISettingService settingService,
            IStaticCacheManager cacheManager,
            IStoreContext storeContext,
            IStoreService storeService,
            ISynchronizationRecordService synchronizationRecordService,
            MailChimpManager mailChimpManager)
        {
            _localizationService = localizationService;
            _notificationService = notificationService;
            _scheduleTaskService = scheduleTaskService;
            _settingService = settingService;
            _staticCacheManager = cacheManager;
            _storeContext = storeContext;
            _storeService = storeService;
            _synchronizationRecordService = synchronizationRecordService;
            _mailChimpManager = mailChimpManager;
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Configure()
        {
            //load settings for a chosen store scope
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var mailChimpSettings = await _settingService.LoadSettingAsync<MailChimpSettings>(storeId);

            //prepare model
            var model = new ConfigurationModel
            {
                ApiKey = mailChimpSettings.ApiKey,
                PassEcommerceData = mailChimpSettings.PassEcommerceData,
                ListId = mailChimpSettings.ListId,
                ListId_OverrideForStore = storeId > 0 && await _settingService.SettingExistsAsync(mailChimpSettings, settings => settings.ListId, storeId),
                ActiveStoreScopeConfiguration = storeId
            };

            //check whether synchronization is in progress
            model.SynchronizationStarted = (await _staticCacheManager.GetAsync(_staticCacheManager.PrepareKeyForDefaultCache(MailChimpDefaults.OperationNumberCacheKey), () => (int?)null)).HasValue;

            //prepare account info
            if (!string.IsNullOrEmpty(mailChimpSettings.ApiKey))
                model.AccountInfo = await _mailChimpManager.GetAccountInfoAsync();

            //prepare available lists
            if (!string.IsNullOrEmpty(mailChimpSettings.ApiKey))
                model.AvailableLists = await _mailChimpManager.GetAvailableListsAsync() ?? new List<SelectListItem>();

            var defaultListId = mailChimpSettings.ListId;
            if (!model.AvailableLists.Any())
            {
                //add the special item for 'there are no lists' with empty guid value
                model.AvailableLists.Add(new SelectListItem
                {
                    Text = await _localizationService.GetResourceAsync("Plugins.Misc.MailChimp.Fields.List.NotExist"),
                    Value = Guid.Empty.ToString()
                });
                defaultListId = Guid.Empty.ToString();
            }
            else if (string.IsNullOrEmpty(mailChimpSettings.ListId) || mailChimpSettings.ListId.Equals(Guid.Empty.ToString()))
                defaultListId = model.AvailableLists.FirstOrDefault()?.Value;

            //set the default list
            model.ListId = defaultListId;
            mailChimpSettings.ListId = defaultListId;
            await _settingService.SaveSettingOverridablePerStoreAsync(mailChimpSettings, settings => settings.ListId, model.ListId_OverrideForStore, storeId);

            //synchronization task
            var task = await _scheduleTaskService.GetTaskByTypeAsync(MailChimpDefaults.SynchronizationTask);
            if (task != null)
            {
                model.SynchronizationPeriod = task.Seconds / 60 / 60;
                model.AutoSynchronization = task.Enabled;
            }

            return View("~/Plugins/Misc.MailChimp/Views/Configure.cshtml", model);
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("save")]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return await Configure();

            //load settings for a chosen store scope
            var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var mailChimpSettings = await _settingService.LoadSettingAsync<MailChimpSettings>(storeId);

            //update stores if the list was changed
            if (!string.IsNullOrEmpty(model.ListId) && !model.ListId.Equals(Guid.Empty.ToString()) && !model.ListId.Equals(mailChimpSettings.ListId))
            {
                (storeId > 0 ? new[] { storeId } : (await _storeService.GetAllStoresAsync()).Select(store => store.Id)).ToList()
                    .ForEach(id => _synchronizationRecordService.CreateOrUpdateRecordAsync(EntityType.Store, id, OperationType.Update));
            }

            //prepare webhook
            if (!string.IsNullOrEmpty(mailChimpSettings.ApiKey))
            {
                var listId = !string.IsNullOrEmpty(model.ListId) && !model.ListId.Equals(Guid.Empty.ToString()) ? model.ListId : string.Empty;
                var webhookPrepared = await _mailChimpManager.PrepareWebhookAsync(listId);

                //display warning if webhook is not prepared
                if (!webhookPrepared && !string.IsNullOrEmpty(listId))
                    _notificationService.WarningNotification(await _localizationService.GetResourceAsync("Plugins.Misc.MailChimp.Webhook.Warning"));
            }

            //save settings
            mailChimpSettings.ApiKey = model.ApiKey.Trim();
            mailChimpSettings.PassEcommerceData = model.PassEcommerceData;
            mailChimpSettings.ListId = model.ListId;
            await _settingService.SaveSettingAsync(mailChimpSettings, x => x.ApiKey, clearCache: false);
            await _settingService.SaveSettingAsync(mailChimpSettings, x => x.PassEcommerceData, clearCache: false);
            await _settingService.SaveSettingOverridablePerStoreAsync(mailChimpSettings, x => x.ListId, model.ListId_OverrideForStore, storeId, false);
            await _settingService.ClearCacheAsync();

            //create or update synchronization task
            var task = await _scheduleTaskService.GetTaskByTypeAsync(MailChimpDefaults.SynchronizationTask);
            if (task == null)
            {
                task = new ScheduleTask
                {
                    Type = MailChimpDefaults.SynchronizationTask,
                    Name = MailChimpDefaults.SynchronizationTaskName,
                    Seconds = MailChimpDefaults.DefaultSynchronizationPeriod * 60 * 60
                };
                await _scheduleTaskService.InsertTaskAsync(task);
            }

            var synchronizationPeriodInSeconds = model.SynchronizationPeriod * 60 * 60;
            var synchronizationEnabled = model.AutoSynchronization;
            if (task.Enabled != synchronizationEnabled || task.Seconds != synchronizationPeriodInSeconds)
            {
                //task parameters was changed
                task.Enabled = synchronizationEnabled;
                task.Seconds = synchronizationPeriodInSeconds;
                await _scheduleTaskService.UpdateTaskAsync(task);
                _notificationService.WarningNotification(await _localizationService.GetResourceAsync("Plugins.Misc.MailChimp.Fields.AutoSynchronization.Restart"));
            }

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("synchronization")]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Synchronization()
        {
            //ensure that user list for the synchronization is selected
            var mailChimpSettings = await _settingService.LoadSettingAsync<MailChimpSettings>();
            if (string.IsNullOrEmpty(mailChimpSettings.ListId) || mailChimpSettings.ListId.Equals(Guid.Empty.ToString()))
            {
                _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.Misc.MailChimp.Synchronization.Error"));
                return await Configure();
            }

            //start the synchronization
            var operationNumber = await _mailChimpManager.SynchronizeAsync(true);
            if (operationNumber.HasValue)
            {
                //cache number of operations
                await _staticCacheManager.RemoveAsync(MailChimpDefaults.SynchronizationBatchesCacheKey);
                await _staticCacheManager.SetAsync(_staticCacheManager.PrepareKeyForDefaultCache(MailChimpDefaults.OperationNumberCacheKey), operationNumber.Value);

                _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.Misc.MailChimp.Synchronization.Started"));
            }
            else
                _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.Misc.MailChimp.Synchronization.Error"));

            return await Configure();
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> IsSynchronizationComplete()
        {
            //try to get number of operations and already handled batches
            var operationNumber = await _staticCacheManager.GetAsync(_staticCacheManager.PrepareKeyForDefaultCache(MailChimpDefaults.OperationNumberCacheKey), () => (int?)null);
            var batchesInfo = await _staticCacheManager.GetAsync(_staticCacheManager.PrepareKeyForDefaultCache(MailChimpDefaults.SynchronizationBatchesCacheKey), () => new Dictionary<string, int>());

            //check whether the synchronization is finished
            if (!operationNumber.HasValue || operationNumber.Value == batchesInfo.Values.Sum())
            {
                //clear cached values
                await _staticCacheManager.RemoveAsync(MailChimpDefaults.OperationNumberCacheKey);
                await _staticCacheManager.RemoveAsync(MailChimpDefaults.SynchronizationBatchesCacheKey);

                return Json(true);
            }

            return new NullJsonResult();
        }

        public IActionResult BatchWebhook()
        {
            return Ok();
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> BatchWebhook(IFormCollection form)
        {
            if (!Request.Form?.Any() ?? true)
                return BadRequest();

            //try to get already handled batches
            var batchesInfo = await _staticCacheManager.GetAsync(_staticCacheManager.PrepareKeyForDefaultCache(MailChimpDefaults.SynchronizationBatchesCacheKey), () => new Dictionary<string, int>());

            //handle batch webhook
            var (id, completedOperationNumber) = await _mailChimpManager.HandleBatchWebhookAsync(Request.Form, batchesInfo);
            if (!string.IsNullOrEmpty(id) && completedOperationNumber.HasValue)
            {
                if (!batchesInfo.ContainsKey(id))
                {
                    //update cached value
                    batchesInfo.Add(id, completedOperationNumber.Value);
                    await _staticCacheManager.SetAsync(_staticCacheManager.PrepareKeyForDefaultCache(MailChimpDefaults.SynchronizationBatchesCacheKey), batchesInfo);
                }
                return Ok();
            }

            return BadRequest();
        }

        public IActionResult WebHook()
        {
            return Ok();
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> WebHook(IFormCollection form)
        {
            if (!Request.Form?.Any() ?? true)
                return BadRequest();

            //handle webhook
            var success = await _mailChimpManager.HandleWebhookAsync(Request.Form);
            return success ? Ok() : (IActionResult)BadRequest();
        }

        #endregion
    }
}