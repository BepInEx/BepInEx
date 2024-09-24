using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using Mono.Cecil;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace BepInEx;

#region BaseUnityPlugin

/// <summary>
///     This attribute denotes that a class is a plugin, and specifies the required metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class BepInPlugin : Attribute
{
    /// <param name="GUID">The unique identifier of the plugin. Should not change between plugin versions.</param>
    /// <param name="Name">The user friendly name of the plugin. Is able to be changed between versions.</param>
    /// <param name="Version">The specific version of the plugin.</param>
    public BepInPlugin(string GUID, string Name, string Version)
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

    internal static BepInPlugin FromCecilType(TypeDefinition td)
    {
        var attr = MetadataHelper.GetCustomAttributes<BepInPlugin>(td, false).FirstOrDefault();

        if (attr == null)
            return null;

        return new BepInPlugin((string) attr.ConstructorArguments[0].Value,
                               (string) attr.ConstructorArguments[1].Value,
                               (string) attr.ConstructorArguments[2].Value);
    }
}

/// <summary>
///     This attribute specifies any dependencies that this plugin has on other plugins.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class BepInDependency : Attribute, ICacheable
{
    /// <summary>
    ///     Flags that are applied to a dependency
    /// </summary>
    [Flags]
    public enum DependencyFlags
    {
        /// <summary>
        ///     The plugin has a hard dependency on the referenced plugin, and will not run without it.
        /// </summary>
        HardDependency = 1,

        /// <summary>
        ///     This plugin has a soft dependency on the referenced plugin, and is able to run without it.
        /// </summary>
        SoftDependency = 2
    }

    /// <summary>
    ///     Marks this <see cref="BaseUnityPlugin" /> as dependent on another plugin. The other plugin will be loaded before
    ///     this one.
    ///     If the other plugin doesn't exist, what happens depends on the <see cref="Flags" /> parameter.
    /// </summary>
    /// <param name="DependencyGUID">The GUID of the referenced plugin.</param>
    /// <param name="Flags">The flags associated with this dependency definition.</param>
    public BepInDependency(string DependencyGUID, DependencyFlags Flags = DependencyFlags.HardDependency)
    {
        this.DependencyGUID = DependencyGUID;
        this.Flags = Flags;
        VersionRange = null;
    }

    /// <summary>
    ///     Marks this <see cref="BaseUnityPlugin" /> as dependent on another plugin. The other plugin will be loaded before
    ///     this one.
    ///     If the other plugin doesn't exist or is of a version not satisfying <see cref="VersionRange" />, this plugin will
    ///     not load and an error will be logged instead.
    /// </summary>
    /// <param name="guid">The GUID of the referenced plugin.</param>
    /// <param name="version">The version range of the referenced plugin.</param>
    /// <remarks>When version is supplied the dependency is always treated as HardDependency</remarks>
    public BepInDependency(string guid, string version) : this(guid)
    {
        VersionRange = Range.Parse(version);
    }

    /// <summary>
    ///     The GUID of the referenced plugin.
    /// </summary>
    public string DependencyGUID { get; protected set; }

    /// <summary>
    ///     The flags associated with this dependency definition.
    /// </summary>
    public DependencyFlags Flags { get; protected set; }

    /// <summary>
    ///     The version <see cref="SemVer.Range">range</see> of the referenced plugin.
    /// </summary>
    public Range VersionRange { get; protected set; }

    void ICacheable.Save(BinaryWriter bw)
    {
        bw.Write(DependencyGUID);
        bw.Write((int) Flags);
        bw.Write(VersionRange?.ToString() ?? string.Empty);
    }

    void ICacheable.Load(BinaryReader br)
    {
        DependencyGUID = br.ReadString();
        Flags = (DependencyFlags) br.ReadInt32();

        var versionRange = br.ReadString();
        VersionRange = versionRange == string.Empty ? null : Range.Parse(versionRange);
    }

    internal static IEnumerable<BepInDependency> FromCecilType(TypeDefinition td)
    {
        var attrs = MetadataHelper.GetCustomAttributes<BepInDependency>(td, true);
        return attrs.Select(customAttribute =>
        {
            var dependencyGuid = (string) customAttribute.ConstructorArguments[0].Value;
            var secondArg = customAttribute.ConstructorArguments[1].Value;
            if (secondArg is string minVersion) return new BepInDependency(dependencyGuid, minVersion);
            return new BepInDependency(dependencyGuid, (DependencyFlags) secondArg);
        }).ToList();
    }
}

/// <summary>
///     This attribute specifies other plugins that are incompatible with this plugin.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class BepInIncompatibility : Attribute, ICacheable
{
    /// <summary>
    ///     Marks this <see cref="BaseUnityPlugin" /> as incompatible with another plugin.
    ///     If the other plugin exists, this plugin will not be loaded and a warning will be shown.
    /// </summary>
    /// <param name="IncompatibilityGUID">The GUID of the referenced plugin.</param>
    public BepInIncompatibility(string IncompatibilityGUID)
    {
        this.IncompatibilityGUID = IncompatibilityGUID;
    }

    /// <summary>
    ///     The GUID of the referenced plugin.
    /// </summary>
    public string IncompatibilityGUID { get; protected set; }

    void ICacheable.Save(BinaryWriter bw) => bw.Write(IncompatibilityGUID);

    void ICacheable.Load(BinaryReader br) => IncompatibilityGUID = br.ReadString();

    internal static IEnumerable<BepInIncompatibility> FromCecilType(TypeDefinition td)
    {
        var attrs = MetadataHelper.GetCustomAttributes<BepInIncompatibility>(td, true);
        return attrs.Select(customAttribute =>
        {
            var dependencyGuid = (string) customAttribute.ConstructorArguments[0].Value;
            return new BepInIncompatibility(dependencyGuid);
        }).ToList();
    }
}

/// <summary>
///     This attribute specifies which processes this plugin should be run for. Not specifying this attribute will load the
///     plugin under every process.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class BepInProcess : Attribute
{
    /// <param name="ProcessName">The name of the process that this plugin will run under.</param>
    public BepInProcess(string ProcessName)
    {
        this.ProcessName = ProcessName;
    }

    /// <summary>
    ///     The name of the process that this plugin will run under.
    /// </summary>
    public string ProcessName { get; protected set; }

    internal static List<BepInProcess> FromCecilType(TypeDefinition td)
    {
        var attrs = MetadataHelper.GetCustomAttributes<BepInProcess>(td, true);
        return attrs.Select(customAttribute =>
                                new BepInProcess((string) customAttribute.ConstructorArguments[0].Value)).ToList();
    }
}

#endregion

#region MetadataHelper

/// <summary>
///     Helper class to use for retrieving metadata about a plugin, defined as attributes.
/// </summary>
public static class MetadataHelper
{
    internal static IEnumerable<CustomAttribute> GetCustomAttributes<T>(TypeDefinition td, bool inherit)
        where T : Attribute
    {
        var result = new List<CustomAttribute>();
        var type = typeof(T);
        var currentType = td;

        do
        {
            result.AddRange(currentType.CustomAttributes.Where(ca => ca.AttributeType.FullName == type.FullName));
            currentType = currentType.BaseType?.Resolve();
        } while (inherit && currentType?.FullName != "System.Object");


        return result;
    }

    /// <summary>
    ///     Retrieves the BepInPlugin metadata from a plugin type.
    /// </summary>
    /// <param name="pluginType">The plugin type.</param>
    /// <returns>The BepInPlugin metadata of the plugin type.</returns>
    public static BepInPlugin GetMetadata(Type pluginType)
    {
        var attributes = pluginType.GetCustomAttributes(typeof(BepInPlugin), false);

        if (attributes.Length == 0)
            return null;

        return (BepInPlugin) attributes[0];
    }

    /// <summary>
    ///     Retrieves the BepInPlugin metadata from a plugin instance.
    /// </summary>
    /// <param name="plugin">The plugin instance.</param>
    /// <returns>The BepInPlugin metadata of the plugin instance.</returns>
    public static BepInPlugin GetMetadata(object plugin) => GetMetadata(plugin.GetType());

    /// <summary>
    ///     Gets the specified attributes of a type, if they exist.
    /// </summary>
    /// <typeparam name="T">The attribute type to retrieve.</typeparam>
    /// <param name="pluginType">The plugin type.</param>
    /// <returns>The attributes of the type, if existing.</returns>
    public static T[] GetAttributes<T>(Type pluginType) where T : Attribute =>
        (T[]) pluginType.GetCustomAttributes(typeof(T), true);

    /// <summary>
    ///     Gets the specified attributes of an assembly, if they exist.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <typeparam name="T">The attribute type to retrieve.</typeparam>
    /// <returns>The attributes of the type, if existing.</returns>
    public static T[] GetAttributes<T>(Assembly assembly) where T : Attribute =>
        (T[]) assembly.GetCustomAttributes(typeof(T), true);

    /// <summary>
    ///     Gets the specified attributes of an instance, if they exist.
    /// </summary>
    /// <typeparam name="T">The attribute type to retrieve.</typeparam>
    /// <param name="plugin">The plugin instance.</param>
    /// <returns>The attributes of the instance, if existing.</returns>
    public static IEnumerable<T> GetAttributes<T>(object plugin) where T : Attribute =>
        GetAttributes<T>(plugin.GetType());

    /// <summary>
    ///     Gets the specified attributes of a reflection metadata type, if they exist.
    /// </summary>
    /// <typeparam name="T">The attribute type to retrieve.</typeparam>
    /// <param name="member">The reflection metadata instance.</param>
    /// <returns>The attributes of the instance, if existing.</returns>
    public static T[] GetAttributes<T>(MemberInfo member) where T : Attribute =>
        (T[]) member.GetCustomAttributes(typeof(T), true);

    /// <summary>
    ///     Retrieves the dependencies of the specified plugin type.
    /// </summary>
    /// <param name="plugin">The plugin type.</param>
    /// <returns>A list of all plugin types that the specified plugin type depends upon.</returns>
    public static IEnumerable<BepInDependency> GetDependencies(Type plugin) =>
        plugin.GetCustomAttributes(typeof(BepInDependency), true).Cast<BepInDependency>();
}

#endregion
