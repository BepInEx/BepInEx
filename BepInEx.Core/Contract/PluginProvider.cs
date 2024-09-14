using System.Collections.Generic;
using System.Reflection;

namespace BepInEx;

/// <summary>
///     The base provider type that is used by the BepInEx plugin loader.
/// </summary>
public abstract class PluginProvider : Plugin
{
    internal PluginProvider() { }

    /// <summary>
    ///     Obtains a list of assemblies containing plugins to load
    /// </summary>
    /// <returns>A list of load context, one per assembly</returns>
    public abstract IList<IPluginLoadContext> GetLoadContexts();

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
