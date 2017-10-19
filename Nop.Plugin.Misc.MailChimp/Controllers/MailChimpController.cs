using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using tasks = System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Tasks;
using Nop.Plugin.Misc.MailChimp.Domain;
using Nop.Plugin.Misc.MailChimp.Models;
using Nop.Plugin.Misc.MailChimp.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Stores;
using Nop.Services.Tasks;
using Nop.Web.Framework.Controllers;
using Nop.Core.Http.Extensions;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.MailChimp.Controllers
{
    public class MailChimpController : BasePluginController
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly IScheduleTaskService _scheduleTaskService;
        private readonly ISettingService _settingService;
        private readonly IStoreService _storeService;
        private readonly ISynchronizationRecordService _synchronizationRecordService;
        private readonly IWorkContext _workContext;
        private readonly MailChimpManager _mailChimpManager;

        #endregion

        #region Ctor

        public MailChimpController(ILocalizationService localizationService,
            IScheduleTaskService scheduleTaskService,
            ISettingService settingService,
            IStoreService storeService,
            ISynchronizationRecordService synchronizationRecordService,
            IWorkContext workContext,
            MailChimpManager mailChimpManager)
        {
            this._localizationService = localizationService;
            this._scheduleTaskService = scheduleTaskService;
            this._settingService = settingService;
            this._storeService = storeService;
            this._synchronizationRecordService = synchronizationRecordService;
            this._workContext = workContext;
            this._mailChimpManager = mailChimpManager;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Get auto synchronization task
        /// </summary>
        /// <returns>Task</returns>
        protected ScheduleTask FindScheduledTask()
        {
            return _scheduleTaskService.GetTaskByType("Nop.Plugin.Misc.MailChimp.Services.MailChimpSynchronizationTask, Nop.Plugin.Misc.MailChimp");
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeId = GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var mailChimpSettings = _settingService.LoadSetting<MailChimpSettings>(storeId);

            var model = new MailChimpModel
            {
                ApiKey = mailChimpSettings.ApiKey,
                UseEcommerceApi = mailChimpSettings.UseEcommerceApi,
                ListId = mailChimpSettings.ListId,
                ActiveStoreScopeConfiguration = storeId
            };

            if (storeId > 0)
                model.ListId_OverrideForStore = _settingService.SettingExists(mailChimpSettings, x => x.ListId, storeId);

            //synchronization task
            var task = FindScheduledTask();
            if (task != null)
            {
                model.AutoSyncEachMinutes = task.Seconds / 60;
                model.AutoSync = task.Enabled;
            }

            //get account info
            //we use Task.Run() because child actions cannot be run asynchronously
            model.AccountInfo = tasks.Task.Run(() => _mailChimpManager.GetAccountInfo()).Result;

            //prepare available lists
            model.AvailableLists = tasks.Task.Run(() => _mailChimpManager.GetAvailableLists()).Result;

            return View("~/Plugins/Misc.MailChimp/Views/Configure.cshtml", model);
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("save")]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(MailChimpModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeId = GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var mailChimpSettings = _settingService.LoadSetting<MailChimpSettings>(storeId);

            //update stores if the list was changed
            if (model.ListId != null && !model.ListId.Equals("0") && !model.ListId.Equals(mailChimpSettings.ListId))
            {
                if (storeId > 0)
                    _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Store, storeId, ActionType.Update);
                else
                    foreach (var store in _storeService.GetAllStores())
                    {
                        _synchronizationRecordService.CreateOrUpdateRecord(EntityType.Store, store.Id, ActionType.Update);
                    }
            }

            //webhook
            if (!string.IsNullOrEmpty(model.ListId))
            {
                //delete current webhook
                if (!model.ListId.Equals(mailChimpSettings.ListId))
                {
                    _mailChimpManager.DeleteWebhook(mailChimpSettings.ListId, mailChimpSettings.WebhookId);
                    mailChimpSettings.WebhookId = string.Empty;
                }

                //and create new one
                if (!model.ListId.Equals("0"))
                {
                    //we use Task.Run() because child actions cannot be run asynchronously
                    mailChimpSettings.WebhookId = tasks.Task.Run(() => _mailChimpManager.CreateWebhook(model.ListId, mailChimpSettings.WebhookId)).Result;
                    if (string.IsNullOrEmpty(mailChimpSettings.WebhookId))
                        ErrorNotification(_localizationService.GetResource("Plugins.Misc.MailChimp.WebhookError"));
                }
            }

            //settings
            mailChimpSettings.ApiKey = model.ApiKey;
            mailChimpSettings.UseEcommerceApi = model.UseEcommerceApi;
            mailChimpSettings.ListId = model.ListId;
            _settingService.SaveSetting(mailChimpSettings, x => x.ApiKey, 0, false);
            _settingService.SaveSetting(mailChimpSettings, x => x.UseEcommerceApi, 0, false);
            _settingService.SaveSettingOverridablePerStore(mailChimpSettings, x => x.ListId, model.ListId_OverrideForStore, storeId, false);
            _settingService.SaveSettingOverridablePerStore(mailChimpSettings, x => x.WebhookId, true, storeId, false);

            //now clear settings cache
            _settingService.ClearCache();

            //create or update synchronization task
            var task = FindScheduledTask();
            if (task != null)
            {
                //task parameters was changed
                if (task.Enabled != model.AutoSync || task.Seconds != model.AutoSyncEachMinutes * 60)
                {
                    task.Enabled = model.AutoSync;
                    task.Seconds = model.AutoSyncEachMinutes * 60;
                    _scheduleTaskService.UpdateTask(task);
                    SuccessNotification(_localizationService.GetResource("Plugins.Misc.MailChimp.AutoSyncRestart"));
                }
                else
                    SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));
            }
            else
            {
                _scheduleTaskService.InsertTask(new ScheduleTask
                {
                    Name = "MailChimp synchronization",
                    Seconds = model.AutoSyncEachMinutes * 60,
                    Enabled = model.AutoSync,
                    Type = "Nop.Plugin.Misc.MailChimp.Services.MailChimpSynchronizationTask, Nop.Plugin.Misc.MailChimp",
                });
                SuccessNotification(_localizationService.GetResource("Plugins.Misc.MailChimp.AutoSyncRestart"));
            }

            return Configure();
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("synchronization")]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Synchronization(MailChimpModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            if (string.IsNullOrEmpty(model.ListId) || model.ListId.Equals("0"))
            {
                ErrorNotification(_localizationService.GetResource("Plugins.Misc.MailChimp.SynchronizationError"));
                return Configure();
            }

            //we use Task.Run() because child actions cannot be run asynchronously
            var batchId = tasks.Task.Run(() => _mailChimpManager.Synchronize()).Result;

            if (!string.IsNullOrEmpty(batchId))
            {
                HttpContext.Session.Set("synchronization", true);
                HttpContext.Session.Set("batchId", batchId);
            }
            else
                ErrorNotification(_localizationService.GetResource("Plugins.Misc.MailChimp.SynchronizationError"));

            return Configure();
        }

        public JsonResult GetSynchronizationInfo()
        {
            if (HttpContext.Session.GetString("batchId") == null)
            {
                HttpContext.Session.Remove("synchronization");
                return Json(new {completed = true, info = string.Empty}); 
            }

            var batchInfo = tasks.Task.Run(() => _mailChimpManager.GetBatchInfo(HttpContext.Session.GetString("batchId"))).Result;

            //batch completed 
            if (batchInfo.Item1)
            {
                HttpContext.Session.Remove("batchId");
                HttpContext.Session.Remove("synchronization");
            }

            return Json(new { completed = batchInfo.Item1, info = batchInfo.Item2 });
        }

        public IActionResult WebHook()
        {
            if (Request.Form.Count > 0)
                _mailChimpManager.WebhookHandler(Request.Form);

            return new StatusCodeResult((int)HttpStatusCode.OK);
        }

        #endregion
    }
}