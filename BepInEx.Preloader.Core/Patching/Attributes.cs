using System;
using System.Linq;
using Mono.Cecil;
using Version = SemanticVersioning.Version;

namespace BepInEx.Preloader.Core.Patching;

/// <summary>
///     This attribute denotes that a class is a patcher plugin, and specifies the required metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class PatcherPluginInfoAttribute : Attribute
{
    /// <param name="GUID">The unique identifier of the plugin. Should not change between plugin versions.</param>
    /// <param name="Name">The user friendly name of the plugin. Is able to be changed between versions.</param>
    /// <param name="Version">The specific version of the plugin.</param>
    public PatcherPluginInfoAttribute(string GUID, string Name, string Version)
    {
        this.GUID = GUID;
        this.Name = Name;
        this.Version = TryParseLongVersion(Version);
    }

    /// <summary>
    ///     The unique identifier of the plugin. Should not change between plugin versions.
    /// </summary>
    public string GUID { get; protected set; }


    /// <summary>
    ///     The user friendly name of the plugin. Is able to be changed between versions.
    /// </summary>
    public string Name { get; protected set; }


    /// <summary>
    ///     The specific version of the plugin.
    /// </summary>
    public Version Version { get; protected set; }

    private static Version TryParseLongVersion(string version)
    {
        if (Version.TryParse(version, out var v))
            return v;

        // no System.Version.TryParse() on .NET 3.5
        try
        {
            var longVersion = new System.Version(version);

            return new Version(longVersion.Major, longVersion.Minor,
                               longVersion.Build != -1 ? longVersion.Build : 0);
        }
        catch { }

        return null;
    }

    internal static PatcherPluginInfoAttribute FromCecilType(TypeDefinition td)
    {
        var attr = MetadataHelper.GetCustomAttributes<PatcherPluginInfoAttribute>(td, false).FirstOrDefault();

        if (attr == null)
            return null;

        return new PatcherPluginInfoAttribute((string) attr.ConstructorArguments[0].Value,
                                              (string) attr.ConstructorArguments[1].Value,
                                              (string) attr.ConstructorArguments[2].Value);
    }

    internal static PatcherPluginInfoAttribute FromType(Type type)
    {
        var attributes = type.GetCustomAttributes(typeof(PatcherPluginInfoAttribute), false);

        if (attributes.Length == 0)
            return null;

        return (PatcherPluginInfoAttribute) attributes[0];
    }
}

/// <summary>
///     Defines an assembly that a patch method will target.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class TargetAssemblyAttribute : Attribute
{
    /// <summary>
    ///     Marker used to indicate all possible assemblies to be targeted by a patch method.
    /// </summary>
    public const string AllAssemblies = "_all";

    /// <param name="targetAssembly">
    ///     The short filename of the assembly. Use <see cref="AllAssemblies" /> to mark all possible
    ///     assemblies as targets.
    /// </param>
    public TargetAssemblyAttribute(string targetAssembly)
    {
        TargetAssembly = targetAssembly;
    }

    /// <summary>
    ///     The short filename of the assembly to target.
    /// </summary>
    public string TargetAssembly { get; }
}

/// <summary>
///     Defines a type that a patch method will target.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class TargetTypeAttribute : Attribute
{
    /// <param name="targetAssembly">The short filename of the assembly of which <paramref name="targetType" /> belongs to.</param>
    /// <param name="targetType">The full name of the type to target for patching.</param>
    public TargetTypeAttribute(string targetAssembly, string targetType)
    {
        TargetAssembly = targetAssembly;
        TargetType = targetType;
    }

    /// <summary>
    ///     The short filename of the assembly to target.
    /// </summary>
    public string TargetAssembly { get; }

    /// <summary>
    ///     The full name of the type to target for patching.
    /// </summary>
    public string TargetType { get; }
}
