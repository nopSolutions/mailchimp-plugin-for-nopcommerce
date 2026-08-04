using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
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
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.MailChimp.Controllers;

[AutoValidateAntiforgeryToken]
[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
public class MailChimpController : BasePluginController
{
    #region Fields

    private readonly ILocalizationService _localizationService;
    private readonly INewsLetterSubscriptionTypeService _newsLetterSubscriptionTypeService;
    private readonly INotificationService _notificationService;
    private readonly IScheduleTaskService _scheduleTaskService;
    private readonly ISettingService _settingService;
    private readonly IStaticCacheManager _staticCacheManager;
    private readonly IStoreContext _storeContext;
    private readonly IStoreService _storeService;
    private readonly MailChimpManager _mailChimpManager;

    #endregion

    #region Ctor

    public MailChimpController(
        ILocalizationService localizationService,
        INewsLetterSubscriptionTypeService newsLetterSubscriptionTypeService,
        INotificationService notificationService,
        IScheduleTaskService scheduleTaskService,
        ISettingService settingService,
        IStaticCacheManager cacheManager,
        IStoreContext storeContext,
        IStoreService storeService,
        MailChimpManager mailChimpManager)
    {
        _localizationService = localizationService;
        _newsLetterSubscriptionTypeService = newsLetterSubscriptionTypeService;
        _notificationService = notificationService;
        _scheduleTaskService = scheduleTaskService;
        _settingService = settingService;
        _staticCacheManager = cacheManager;
        _storeContext = storeContext;
        _storeService = storeService;
        _mailChimpManager = mailChimpManager;
    }

    #endregion

    #region Methods

    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
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
            PassOnlySubscribed = mailChimpSettings.PassOnlySubscribed,
            ActiveStoreScopeConfiguration = storeId
        };

        //check whether synchronization is in progress
        model.SynchronizationStarted = await _staticCacheManager.GetAsync(_staticCacheManager.PrepareKeyForDefaultCache(MailChimpDefaults.OperationNumberCacheKey), () => 0) != 0;

        //prepare account info
        if (!string.IsNullOrEmpty(mailChimpSettings.ApiKey))
            model.AccountInfo = await _mailChimpManager.GetAccountInfoAsync();

        //prepare subscription types from the database
        var newsLetterSubscriptionTypes = await _newsLetterSubscriptionTypeService.GetAllNewsLetterSubscriptionTypesAsync(storeId);

        //map from settings
        var audienceTypeListMaps = await _mailChimpManager.GetAudienceTypeListMapsForStoreAsync(storeId);

        model.NewsLetterSubscriptionTypes = await newsLetterSubscriptionTypes.Select(subscriptionType => new NewsLetterSubscriptionMapModel
        {
            TypeId = subscriptionType.Id,
            Name = subscriptionType.Name,
            ListId = audienceTypeListMaps.Where(x => x.TypeListId == subscriptionType.Id).Select(x => x.AudienceId).FirstOrDefault() ?? Guid.Empty.ToString()
        }).ToListAsync();

        //prepare available lists
        if (!string.IsNullOrEmpty(mailChimpSettings.ApiKey))
            model.AvailableLists = await _mailChimpManager.GetAvailableListsAsync() ?? new List<SelectListItem>();

        if (!model.AvailableLists.Any())
        {
            //add the special item for 'there are no lists' with empty guid value
            model.AvailableLists.Add(new SelectListItem
            {
                Text = await _localizationService.GetResourceAsync("Plugins.Misc.MailChimp.Fields.List.NotExist"),
                Value = Guid.Empty.ToString()
            });
        }

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
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return await Configure();

        //load settings for a chosen store scope
        var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var mailChimpSettings = await _settingService.LoadSettingAsync<MailChimpSettings>(storeId);

        //save settings
        mailChimpSettings.ApiKey = model.ApiKey.Trim();
        mailChimpSettings.PassEcommerceData = model.PassEcommerceData;
        mailChimpSettings.PassOnlySubscribed = model.PassOnlySubscribed;

        //var audienceTypeListMaps = await GetAudienceTypeListMapsForStoreAsync(storeId);

        var audienceTypeListMap = new List<AudienceTypeListMap>();
        foreach (var subscriptionType in model.NewsLetterSubscriptionTypes)
            audienceTypeListMap.Add(new AudienceTypeListMap
            {
                TypeListId = subscriptionType.TypeId,
                AudienceId = subscriptionType.ListId
            });

        mailChimpSettings.SubscriptionTypeMappings = JsonConvert.SerializeObject(audienceTypeListMap);

        await _settingService.SaveSettingAsync(mailChimpSettings, x => x.ApiKey, clearCache: false);
        await _settingService.SaveSettingAsync(mailChimpSettings, x => x.PassEcommerceData, clearCache: false);
        await _settingService.SaveSettingAsync(mailChimpSettings, x => x.PassOnlySubscribed, clearCache: false);
        await _settingService.SaveSettingAsync(mailChimpSettings, settings => settings.SubscriptionTypeMappings, clearCache: false);
        await _settingService.ClearCacheAsync();

        //prepare webhook
        if (!string.IsNullOrEmpty(mailChimpSettings.ApiKey))
        {
            foreach (var mapping in audienceTypeListMap)
            {
                var listId = mapping.AudienceId;
                var webhookPrepared = await _mailChimpManager.PrepareWebhookAsync(listId);

                //display warning if webhook is not prepared
                if (!webhookPrepared && !string.IsNullOrEmpty(listId))
                    _notificationService.WarningNotification(await _localizationService.GetResourceAsync("Plugins.Misc.MailChimp.Webhook.Warning"));
            }
        }

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
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Synchronization()
    {
        //ensure that user list for the synchronization is selected
        var storeId = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var audienceTypeListMaps = await _mailChimpManager.GetAudienceTypeListMapsForStoreAsync(storeId);

        foreach (var mapping in audienceTypeListMaps)
        {
            if (string.IsNullOrEmpty(mapping.AudienceId))
            {
                _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.Misc.MailChimp.Synchronization.Error"));
                return await Configure();
            }
        }

        //start the synchronization
        var operationNumber = await _mailChimpManager.SynchronizeAsync(true);
        if (operationNumber > 0)
        {
            //cache number of operations
            await _staticCacheManager.RemoveAsync(MailChimpDefaults.SynchronizationBatchesCacheKey);
            await _staticCacheManager.SetAsync(_staticCacheManager.PrepareKeyForDefaultCache(MailChimpDefaults.OperationNumberCacheKey), operationNumber);

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Plugins.Misc.MailChimp.Synchronization.Started"));
        }
        else
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.Misc.MailChimp.Synchronization.Error"));

        return await Configure();
    }

    public async Task<IActionResult> IsSynchronizationComplete()
    {
        //try to get number of operations and already handled batches
        var operationNumber = await _staticCacheManager.GetAsync(_staticCacheManager.PrepareKeyForDefaultCache(MailChimpDefaults.OperationNumberCacheKey), () => 0);
        var batchesInfo = await _staticCacheManager.GetAsync(_staticCacheManager.PrepareKeyForDefaultCache(MailChimpDefaults.SynchronizationBatchesCacheKey), () => new Dictionary<string, int>());

        //check whether the synchronization is finished
        if (operationNumber == 0 || operationNumber == batchesInfo.Values.Sum())
        {
            //clear cached values
            await _staticCacheManager.RemoveAsync(MailChimpDefaults.OperationNumberCacheKey);
            await _staticCacheManager.RemoveAsync(MailChimpDefaults.SynchronizationBatchesCacheKey);

            return Json(true);
        }

        return new NullJsonResult();
    }

    #endregion
}