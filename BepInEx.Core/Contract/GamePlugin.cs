namespace BepInEx;

/// <summary>
///     The base plugin type that is used by the BepInEx plugin loader.
/// </summary>
public abstract class GamePlugin : Plugin
{
    /// <summary>
    ///     A callback called when loaded by the chainloader
    /// </summary>
    public abstract void Load();
}
