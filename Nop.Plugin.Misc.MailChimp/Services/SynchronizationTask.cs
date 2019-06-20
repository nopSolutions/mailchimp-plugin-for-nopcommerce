using Nop.Core;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.Tasks;

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
        public void Execute()
        {
            //ensure that plugin installed
            var pluginDescriptor = _pluginService.GetPluginDescriptorBySystemName<IPlugin>(MailChimpDefaults.SystemName, LoadPluginsMode.InstalledOnly);
            if (pluginDescriptor == null)
                return;

            //start the synchronization
            var synchronizationStarted = _mailChimpManager.Synchronize().Result.HasValue;
            if (!synchronizationStarted)
                throw new NopException(_localizationService.GetResource("Plugins.Misc.MailChimp.Synchronization.Error"));
        }

        #endregion
    }
}