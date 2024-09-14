using System;

namespace BepInEx.Preloader.Core.Patching;

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
