namespace BepInEx.Preloader.Core.Patching;

/// <summary>
///     A patcher that can contain multiple methods for patching assemblies.
/// </summary>
public abstract class PatcherPlugin : Plugin
{
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
