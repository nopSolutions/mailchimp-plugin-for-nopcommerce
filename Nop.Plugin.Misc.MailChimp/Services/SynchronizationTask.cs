using Nop.Core;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Task = System.Threading.Tasks.Task;

namespace Nop.Plugin.Misc.MailChimp.Services
{
    /// <summary>
    /// Represents a task that synchronizes data with MailChimp
    /// </summary>
    public class SynchronizationTask : IScheduleTask
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly IPluginService _pluginService;
        private readonly MailChimpManager _mailChimpManager;

        #endregion

        #region Ctor

        public SynchronizationTask(ILocalizationService localizationService,
            IPluginService pluginService,
            MailChimpManager mailChimpManager)
        {
            _localizationService = localizationService;
            _pluginService = pluginService;
            _mailChimpManager = mailChimpManager;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Execute task
        /// </summary>
        public async Task ExecuteAsync()
        {
            //ensure that plugin installed
            var pluginDescriptor = await _pluginService.GetPluginDescriptorBySystemNameAsync<IPlugin>(MailChimpDefaults.SystemName, LoadPluginsMode.InstalledOnly);
            if (pluginDescriptor == null)
                return;

            //start the synchronization
            var synchronizationStarted = (await _mailChimpManager.SynchronizeAsync()).HasValue;
            if (!synchronizationStarted)
                throw new NopException(await _localizationService.GetResourceAsync("Plugins.Misc.MailChimp.Synchronization.Error"));
        }

        #endregion
    }
}