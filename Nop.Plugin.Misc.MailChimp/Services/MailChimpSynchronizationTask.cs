using Nop.Core.Plugins;
using Nop.Services.Tasks;

namespace Nop.Plugin.Misc.MailChimp.Services
{
    public class MailChimpSynchronizationTask : ITask
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
            if (pluginDescriptor == null)
                return;

            var plugin = pluginDescriptor.Instance() as MailChimpPlugin;
            if (plugin == null)
                return;

            plugin.Synchronize();
        }
    }
}