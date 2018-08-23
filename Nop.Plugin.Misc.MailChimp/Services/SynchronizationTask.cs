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
        private readonly IPluginFinder _pluginFinder;
        private readonly MailChimpManager _mailChimpManager;

        #endregion

        #region Ctor

        public SynchronizationTask(ILocalizationService localizationService,
            IPluginFinder pluginFinder,
            MailChimpManager mailChimpManager)
        {
            this._localizationService = localizationService;
            this._pluginFinder = pluginFinder;
            this._mailChimpManager = mailChimpManager;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Execute task
        /// </summary>
        public void Execute()
        {
            //ensure that plugin installed
            var plugin = _pluginFinder.GetPluginDescriptorBySystemName(MailChimpDefaults.SystemName);
            if (!(plugin?.Installed ?? false) || !(plugin.Instance() is MailChimpPlugin))
                return;

            //start the synchronization
            var synchronizationStarted = _mailChimpManager.Synchronize().Result.HasValue;
            if (!synchronizationStarted)
                throw new NopException(_localizationService.GetResource("Plugins.Misc.MailChimp.Synchronization.Error"));
        }

        #endregion
    }
}