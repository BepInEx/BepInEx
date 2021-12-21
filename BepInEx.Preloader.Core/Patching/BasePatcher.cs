using BepInEx.Configuration;
using BepInEx.Logging;

namespace BepInEx.Preloader.Core.Patching;

/// <summary>
///     A patcher that can contain multiple methods for patching assemblies.
/// </summary>
public abstract class BasePatcher
{
    protected BasePatcher()
    {
        Info = PatcherPluginInfoAttribute.FromType(GetType());

        Log = Logger.CreateLogSource(Info.Name);

        Config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, Info.GUID + ".cfg"), false,
                                new BepInPlugin(Info.GUID, Info.Name, Info.Version.ToString()));
    }

    /// <summary>
    ///     A <see cref="ILogSource" /> instance created for use by this patcher plugin.
    /// </summary>
    public ManualLogSource Log { get; }

    /// <summary>
    ///     A configuration file binding created with the <see cref="PatcherPluginInfoAttribute.GUID" /> of this plugin as the
    ///     filename.
    /// </summary>
    public ConfigFile Config { get; }

    /// <summary>
    ///     Metadata associated with this patcher plugin.
    /// </summary>
    public PatcherPluginInfoAttribute Info { get; }

    /// <summary>
    ///     The context of the <see cref="AssemblyPatcher" /> this BasePatcher is associated with.
    /// </summary>
    public PatcherContext Context { get; set; }

    /// <summary>
    ///     Executed before any patches from any plugin are applied.
    /// </summary>
    public virtual void Initialize() { }

    /// <summary>
    ///     Executed after all patches from all plugins have been applied.
    /// </summary>
    public virtual void Finalizer() { }
}
