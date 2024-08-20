using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace BepInEx.Preloader.Core.Patching;

/// <summary>
///     A patcher provider that can discover and load assemblies containing patcher plugins
/// </summary>
public abstract class BasePatcherProvider
{
    protected BasePatcherProvider()
    {
        Info = PatcherPluginInfoAttribute.FromType(GetType());

        Log = Logger.CreateLogSource(Info.Name);

        Config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, Info.GUID + ".cfg"), false,
                                new BepInPlugin(Info.GUID, Info.Name, Info.Version.ToString()));
    }

    /// <summary>
    ///     A <see cref="ILogSource" /> instance created for use by this patcher plugin provider.
    /// </summary>
    public ManualLogSource Log { get; }

    /// <summary>
    ///     A configuration file binding created with the <see cref="PatcherPluginInfoAttribute.GUID" /> of this provider as the
    ///     filename.
    /// </summary>
    public ConfigFile Config { get; }

    /// <summary>
    ///     Metadata associated with this patcher plugin provider.
    /// </summary>
    public PatcherPluginInfoAttribute Info { get; }
    
    /// <summary>
    ///     Obtains a list of assemblies containing patchers to load
    /// </summary>
    /// <returns>A list of load context, one per assembly</returns>
    public abstract IList<IPluginLoadContext> GetPatchers();

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
