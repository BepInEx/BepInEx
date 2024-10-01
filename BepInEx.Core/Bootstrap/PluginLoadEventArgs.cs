using System;
using System.Reflection;

namespace BepInEx.Core.Bootstrap;

/// <summary>
///     Info about the loading of a plugin
/// </summary>
public class PluginLoadEventArgs : EventArgs
{
    /// <summary>
    ///     The concerned plugin
    /// </summary>
    public PluginInfo PluginInfo { get; internal set; }
    
    /// <summary>
    ///     The plugin's assembly
    /// </summary>
    public Assembly Assembly { get; internal set; }
    
    /// <summary>
    ///     The plugin's instance
    /// </summary>
    public Plugin PluginInstance { get; internal set; }
    
    internal PluginLoadEventArgs(PluginInfo pluginInfo, Assembly assembly, Plugin pluginInstance)
    {
        PluginInfo = pluginInfo;
        Assembly = assembly;
        PluginInstance = pluginInstance;
    }
}
