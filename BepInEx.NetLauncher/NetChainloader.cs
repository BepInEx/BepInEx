using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace BepInEx.NetLauncher
{
    public class NetChainloader : BaseChainloader<BasePlugin>
    {
        public override BasePlugin LoadPlugin(PluginInfo pluginInfo, Assembly pluginAssembly)
        {
            var type = pluginAssembly.GetType(pluginInfo.TypeName);

            var pluginInstance = (BasePlugin) Activator.CreateInstance(type);

            pluginInstance.Load();

            return pluginInstance;
        }
    }
}
