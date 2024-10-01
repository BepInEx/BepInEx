using System;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil;

namespace BepInEx.Preloader.Core.Patching;

/// <summary>
///     A definition of an individual patch for use by <see cref="AssemblyPatcherMethod" />.
/// </summary>
public class PatchDefinition
{
    /// <summary>
    ///     Creates a patch definition that targets an assembly
    /// </summary>
    /// <param name="targetAssembly">The name of the assembly</param>
    /// <param name="instance">The plugin instance of this patch definition</param>
    /// <param name="assemblyPatcher">The patching method</param>
    public PatchDefinition(string targetAssembly, Plugin instance, AssemblyPatcher assemblyPatcher)
    {
        TargetAssembly = targetAssembly;
        TargetType = null;
        Instance = instance;
        AssemblyPatcherMethod = assemblyPatcher;
        TypePatcherMethod = null;

        FullName = $"{instance.Info.Metadata.Name}/{assemblyPatcher.Method.Name} -> {TargetAssembly}";
    }

    /// <summary>
    ///     Creates a patch definition that targets a type within an assembly
    /// </summary>
    /// <param name="targetAssembly">The name of the assembly</param>
    /// <param name="targetType">The name of the type</param>
    /// <param name="instance">The plugin instance of this patch definition</param>
    /// <param name="typePatcher">The patching method</param>
    public PatchDefinition(string targetAssembly, string targetType, Plugin instance, TypePatcher typePatcher)
    {
        TargetAssembly = targetAssembly;
        TargetType = targetType;
        Instance = instance;
        AssemblyPatcherMethod = null;
        TypePatcherMethod = typePatcher;

        FullName =
            $"{instance.Info.Metadata.Name}/{typePatcher.Method.Name} -> {targetAssembly}/{TargetType}";
    }

    /// <summary>
    ///     The assembly / assemblies this patch will target, if there are any.
    /// </summary>
    public string TargetAssembly { get; }

    /// <summary>
    ///     The type / types this patch will target, if there are any.
    /// </summary>
    public string TargetType { get; }

    /// <summary>
    ///     The instance of the <see cref="Plugin" /> this <see cref="PatchDefinition" /> originates from.
    /// </summary>
    public Plugin Instance { get; }

    /// <summary>
    ///     A method that patches an assembly given a <see cref="PatcherContext"/> and an <see cref="AssemblyDefinition"/>.
    ///     Also receives the .dll filename of the assembly and returns whether the assembly was patched
    /// </summary>
    public delegate bool AssemblyPatcher(PatcherContext context, AssemblyDefinition assemblyDefinition, string targetDll);
    
    /// <summary>
    ///     The method that will perform the patching logic defined by this <see cref="PatchDefinition" /> instance on an assembly.
    /// </summary>
    public AssemblyPatcher AssemblyPatcherMethod { get; }

    /// <summary>
    ///     A method that patches an assembly given a <see cref="PatcherContext"/> and an <see cref="AssemblyDefinition"/>.
    ///     Also receives the .dll filename of the assembly and returns whether the assembly was patched
    /// </summary>
    public delegate bool TypePatcher(PatcherContext context, TypeDefinition assemblyDefinition, string targetDll);

    /// <summary>
    ///     The method that will perform the patching logic defined by this <see cref="PatchDefinition" /> instance on a type.
    /// </summary>
    public TypePatcher TypePatcherMethod { get; }

    /// <summary>
    ///     A friendly name for this patch definition, for use in logging and error tracking.
    /// </summary>
    public string FullName { get; }
}

/// <summary>
///     Context provided to patcher plugins from the associated patcher engine.
/// </summary>
public class PatcherContext
{
    /// <summary>
    ///     <para>Contains a list of assemblies that will be patched and loaded into the runtime.</para>
    ///     <para>
    ///         The dictionary has the name of the file, without any directories. These are used by the dumping
    ///         functionality, and as such, these are also required to be unique. They do not have to be exactly the same as
    ///         the real filename, however they have to be mapped deterministically.
    ///     </para>
    ///     <para>Order is not respected, as it will be sorted by dependencies.</para>
    /// </summary>
    public Dictionary<string, AssemblyDefinition> AvailableAssemblies { get; } = new();

    /// <summary>
    ///     <para>Contains a mapping of available assembly name to their original filenames.</para>
    /// </summary>
    public Dictionary<string, string> AvailableAssembliesPaths { get; } = new();

    /// <summary>
    ///     <para>Contains a dictionary of assemblies that have been loaded as part of executing this assembly patcher.</para>
    ///     <para>
    ///         The key is the same key as used in <see cref="LoadedAssemblies" />, while the value is the actual assembly
    ///         itself.
    ///     </para>
    /// </summary>
    public Dictionary<string, Assembly> LoadedAssemblies { get; } = new();

    /// <summary>
    ///     A list of individual patches that <see cref="AssemblyPatcher" /> will execute.
    /// </summary>
    public List<PatchDefinition> PatchDefinitions { get; } = new();

    /// <summary>
    ///     The directory location as to where patched assemblies will be saved to and loaded from disk, for debugging
    ///     purposes. Defaults to BepInEx/DumpedAssemblies.
    /// </summary>
    public string DumpedAssembliesPath { get; internal set; }
}
