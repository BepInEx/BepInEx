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

    /// <inheritdoc />
    public void Save(BinaryWriter bw) => bw.Write(TypeName);

    /// <inheritdoc />
    public void Load(BinaryReader br) => TypeName = br.ReadString();
}
