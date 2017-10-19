using Nop.Core.Plugins;
using Nop.Services.Tasks;

namespace Nop.Plugin.Misc.MailChimp.Services
{
    public class MailChimpSynchronizationTask : IScheduleTask
    {
        private readonly IPluginFinder _pluginFinder;

        public MailChimpSynchronizationTask(IPluginFinder pluginFinder)
        {
            this._pluginFinder = pluginFinder;
        }

        /// <summary>
        /// Execute task
        /// </summary>
        public void Execute()
        {
            //ensure that plugin exists
            var pluginDescriptor = _pluginFinder.GetPluginDescriptorBySystemName("Misc.MailChimp");

            var plugin = pluginDescriptor?.Instance() as MailChimpPlugin;

            plugin?.Synchronize();
        }
    }
}