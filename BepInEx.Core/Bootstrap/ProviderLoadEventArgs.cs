namespace BepInEx.Core.Bootstrap;

/// <summary>
///     Info about the loading of a provider
/// </summary>
public class ProviderLoadEventArgs
{
    /// <summary>
    ///     The concerned plugin
    /// </summary>
    public PluginInfo PluginInfo { get; internal set; }
    
    /// <summary>
    ///     The plugin's instance
    /// </summary>
    public Plugin PluginInstance { get; internal set; }
    
    internal ProviderLoadEventArgs(PluginInfo pluginInfo, Plugin pluginInstance)
    {
        PluginInfo = pluginInfo;
        PluginInstance = pluginInstance;
    }
}
