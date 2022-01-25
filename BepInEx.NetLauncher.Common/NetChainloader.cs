using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Preloader.Core.Logging;

namespace BepInEx.NetLauncher.Common
{
    public class NetChainloader : BaseChainloader<BasePlugin>
    {
        public override BasePlugin LoadPlugin(PluginInfo pluginInfo, Assembly pluginAssembly)
        {
            var type = pluginAssembly.GetType(pluginInfo.TypeName);

            var pluginInstance = (BasePlugin)Activator.CreateInstance(type);

            pluginInstance.Load();

            return pluginInstance;
        }

        protected override void InitializeLoggers()
        {
            base.InitializeLoggers();

            ChainloaderLogHelper.RewritePreloaderLogs();
        }
    }
}
