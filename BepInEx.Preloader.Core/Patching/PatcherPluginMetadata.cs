using System.IO;
using BepInEx.Bootstrap;

namespace BepInEx.Preloader.Core.Patching;

/// <summary>
///     A single cached assembly patcher.
/// </summary>
internal class PatcherPluginMetadata : ICacheable
{
    /// <summary>
    ///     Type name of the patcher.
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    ///     The loader used to load this patcher, null if it is a provider
    /// </summary>
    public IPluginLoader Loader { get; set; }

    /// <inheritdoc />
    public void Save(BinaryWriter bw) => bw.Write(TypeName);

    /// <inheritdoc />
    public void Load(BinaryReader br) => TypeName = br.ReadString();
}
