using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace BepInEx;

/// <summary>
///     The base plugin provider type that is used by the BepInEx plugin loader.
/// </summary>
public abstract class BasePluginProvider
{
    /// <summary>
    ///     Create a new instance of a plugin provider and all of its tied in objects.
    /// </summary>
    /// <exception cref="InvalidOperationException">BepInPluginProvider attribute is missing.</exception>
    protected BasePluginProvider()
    {
        var metadata = MetadataHelper.GetPluginProviderMetadata(this);
        if (metadata == null)
            throw new InvalidOperationException("Can't create an instance of " + GetType().FullName +
                                                " because it inherits from BasePluginProvider and the BepInPluginProvider attribute is missing.");

        Info = new PluginInfo
        {
            Metadata = metadata,
            Instance = this,
            Dependencies = MetadataHelper.GetDependencies(GetType()),
            Processes = MetadataHelper.GetAttributes<BepInProcess>(GetType()),
        };

        Logger = BepInEx.Logging.Logger.CreateLogSource(metadata.Name);

        Config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, metadata.GUID + ".cfg"), false, metadata);
    }

    /// <summary>
    ///     Information about this plugin provider as it was loaded.
    /// </summary>
    public PluginInfo Info { get; }

    /// <summary>
    ///     Logger instance tied to this plugin provider.
    /// </summary>
    protected ManualLogSource Logger { get; }

    /// <summary>
    ///     Default config file tied to this plugin provider. The config file will not be created until
    ///     any settings are added and changed, or <see cref="ConfigFile.Save" /> is called.
    /// </summary>
    public ConfigFile Config { get; }

    /// <summary>
    ///     Obtains a list of assemblies containing plugins to load
    /// </summary>
    /// <returns>A list of loaders, one per assembly</returns>
    public abstract IList<IPluginLoader> GetPlugins();

    /// <summary>
    ///     A custom assembly resolver that can be used by this provider to resolve assemblies
    ///     that have failed to resolve
    /// </summary>
    /// <param name="name">The assembly's name</param>
    /// <returns>The resolved assembly or null if the assembly couldn't be resolved</returns>
    public virtual Assembly ResolveAssembly(string name)
    {
        return null;
    }
}
