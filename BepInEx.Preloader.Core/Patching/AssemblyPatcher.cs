using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil;

namespace BepInEx.Preloader.Core.Patching;

/// <summary>
///     Worker class which is used for loading and patching entire folders of assemblies, or alternatively patching and
///     loading assemblies one at a time.
/// </summary>
public class AssemblyPatcher : IDisposable
{
    private static readonly ConfigEntry<bool> ConfigDumpAssemblies = ConfigFile.CoreConfig.Bind(
     "Preloader", "DumpAssemblies",
     false,
     "If enabled, BepInEx will save patched assemblies into BepInEx/DumpedAssemblies.\nThis can be used by developers to inspect and debug preloader patchers.");

    private static readonly ConfigEntry<bool> ConfigLoadDumpedAssemblies = ConfigFile.CoreConfig.Bind(
     "Preloader", "LoadDumpedAssemblies",
     false,
     "If enabled, BepInEx will load patched assemblies from BepInEx/DumpedAssemblies instead of memory.\nThis can be used to be able to load patched assemblies into debuggers like dnSpy.\nIf set to true, will override DumpAssemblies.");

    private static readonly ConfigEntry<bool> ConfigBreakBeforeLoadAssemblies = ConfigFile.CoreConfig.Bind(
     "Preloader", "BreakBeforeLoadAssemblies",
     false,
     "If enabled, BepInEx will call Debugger.Break() once before loading patched assemblies.\nThis can be used with debuggers like dnSpy to install breakpoints into patched assemblies before they are loaded.");
    
    private readonly Func<byte[], string, Assembly> assemblyLoader;
    
    /// <summary>
    ///     The current instance of the assembly patcher
    /// </summary>
    public static AssemblyPatcher Instance { get; private set; }

    /// <summary>
    ///     Initialise an <see cref="AssemblyPatcher"/>
    /// </summary>
    /// <param name="directories">The directories paths to search for assemblies</param>
    /// <param name="assemblyExtensions">The filename extensions to search for</param>
    /// <param name="assemblyLoader">A callback that loads the assembly for patching</param>
    internal AssemblyPatcher(IEnumerable<string> directories, IEnumerable<string> assemblyExtensions, Func<byte[], string, Assembly> assemblyLoader)
    {
        Instance = this;
        this.assemblyLoader = assemblyLoader;

        LoadAssemblyDirectories(directories, assemblyExtensions);
        
        Logger.Log(LogLevel.Info, $"{PatcherContext.AvailableAssemblies.Count} assemblies discovered");
    }

    /// <summary>
    ///     The context of this assembly patcher instance that is passed to all patcher plugins.
    /// </summary>
    internal PatcherContext PatcherContext { get; } = new()
    {
        DumpedAssembliesPath = Utility.CombinePaths(Paths.BepInExRootPath, "DumpedAssemblies", Paths.ProcessName)
    };

    private ManualLogSource Logger { get; } = BepInEx.Logging.Logger.CreateLogSource("AssemblyPatcher");

    /// <summary>
    ///     Adds a patch definition to be applied
    /// </summary>
    /// <param name="definition">The patch definition to apply</param>
    public void AddDefinition(PatchDefinition definition)
    {
        Logger.Log(LogLevel.Debug, $"Discovered patch [{definition.FullName}]");
        PatcherContext.PatchDefinitions.Add(definition);
    }

    /// <summary>
    ///     Occurs after all assemblies have been patched
    /// </summary>
    public event Action AllAssembliesPatched;
    
    /// <summary>
    ///     Performs work to dispose collection objects.
    /// </summary>
    public void Dispose()
    {
        foreach (var assembly in PatcherContext.AvailableAssemblies)
            assembly.Value.Dispose();

        PatcherContext.AvailableAssemblies.Clear();

        PatcherContext.AvailableAssembliesPaths.Clear();
    }

    /// <summary>
    ///     Adds all assemblies in given directories to be patched and loaded by this patcher instance.
    ///     Non-managed assemblies are skipped.
    /// </summary>
    /// <param name="directories">The directory to search.</param>
    /// <param name="assemblyExtensions">The file extensions to attempt to load.</param>
    private void LoadAssemblyDirectories(IEnumerable<string> directories, IEnumerable<string> assemblyExtensions)
    {
        var filesToSearch = assemblyExtensions
            .SelectMany(ext => Utility.GetUniqueFilesInDirectories(directories, $"*.{ext}"));

        foreach (var assemblyPath in filesToSearch)
        {
            if (!TryLoadAssembly(assemblyPath, out var assembly))
                continue;

            // NOTE: this is special cased here because the dependency handling for System.dll is a bit wonky
            // System has an assembly reference to itself, and it also has a reference to Mono.Security causing a circular dependency
            // It's also generally dangerous to change system.dll since so many things rely on it, 
            // and it's already loaded into the appdomain since this loader references it, so we might as well skip it
            if (assembly.Name.Name == "System"
             || assembly.Name.Name ==
                "mscorlib") //mscorlib is already loaded into the appdomain so it can't be patched
            {
                assembly.Dispose();
                continue;
            }

            var fileName = Path.GetFileName(assemblyPath);
            PatcherContext.AvailableAssemblies.Add(fileName, assembly);
            PatcherContext.AvailableAssembliesPaths.Add(fileName, assemblyPath);

            Logger.LogDebug($"Assembly loaded: {Path.GetFileName(assemblyPath)}");
        }
    }

    /// <summary>
    ///     Attempts to load a managed assembly as an <see cref="AssemblyDefinition" />. Returns true if successful.
    /// </summary>
    /// <param name="path">The path of the assembly.</param>
    /// <param name="assembly">The loaded assembly. Null if not successful in loading.</param>
    private static bool TryLoadAssembly(string path, out AssemblyDefinition assembly)
    {
        try
        {
            assembly = AssemblyDefinition.ReadAssembly(path, TypeLoader.ReaderParameters);
            return true;
        }
        catch (BadImageFormatException)
        {
            // Not a managed assembly
            assembly = null;
            return false;
        }
    }

    /// <summary>
    ///     Applies patchers to all assemblies loaded into this assembly patcher and then loads patched assemblies into memory.
    /// </summary>
    internal void PatchAndLoad()
    {
        // First, create a copy of the assembly dictionary as the initializer can change them
        var assemblies =
            new Dictionary<string, AssemblyDefinition>(PatcherContext.AvailableAssemblies,
                                                       StringComparer.InvariantCultureIgnoreCase);

        // Perform the actual patching
        var patchedAssemblies = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        var resolvedAssemblies = new Dictionary<string, string>();

        // TODO: Maybe instead reload the assembly and repatch with other valid patchers?
        var invalidAssemblies = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        Logger.Log(LogLevel.Message, $"Executing {PatcherContext.PatchDefinitions.Count} patch(es)");

        foreach (var patchDefinition in PatcherContext.PatchDefinitions.ToList())
        {
            var targetDll = patchDefinition.TargetAssembly;

            var isAssemblyPatch = patchDefinition.TargetType == null;

            if (targetDll == TargetAssemblyAttribute.AllAssemblies)
            {
                foreach (var kv in PatcherContext.AvailableAssemblies.ToList())
                {
                    if (invalidAssemblies.Contains(kv.Key))
                        continue;

                    RunPatcher(kv.Value, kv.Key);
                }
            }
            else
            {
                if (!PatcherContext.AvailableAssemblies.TryGetValue(targetDll, out var assembly)
                 || invalidAssemblies.Contains(targetDll))
                    continue;

                RunPatcher(assembly, targetDll);
            }


            bool RunPatcher(AssemblyDefinition assembly, string targetDll)
            {
                try
                {
                    bool patched = false;
                    if (!isAssemblyPatch)
                    {
                        var targetType =
                            assembly.MainModule.Types.FirstOrDefault(x => x.FullName ==
                                                                          patchDefinition.TargetType);

                        if (targetType == null)
                        {
                            Logger
                                .LogWarning($"Unable to find type [{patchDefinition.TargetType}] defined in {patchDefinition.TypePatcherMethod.Method.Name}. Skipping patcher");
                            return false;
                        }

                        patched = patchDefinition.TypePatcherMethod.Invoke(PatcherContext, targetType, targetDll);
                    }
                    else
                    {
                        patched = patchDefinition.AssemblyPatcherMethod.Invoke(PatcherContext, assembly, targetDll);
                    }

                    if (patched)
                    {
                        if (isAssemblyPatch)
                        {
                            PatcherContext.AvailableAssemblies[targetDll] = assembly;
                        }

                        patchedAssemblies.Add(targetDll);
                    }

                    return true;
                }
                catch (Exception e)
                {
                    Logger.Log(LogLevel.Error,
                               $"Failed to run [{patchDefinition.FullName}] when patching [{assembly.Name.Name}]. This assembly will not be patched. Error: {e}");
                    patchedAssemblies.Remove(targetDll);
                    invalidAssemblies.Add(targetDll);
                    return false;
                }
            }


            foreach (var resolvedAss in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = Utility.TryParseAssemblyName(resolvedAss.FullName, out var assName)
                               ? assName.Name
                               : resolvedAss.FullName;

                // Report only the first type that caused the assembly to load, because any subsequent ones can be false positives
                if (!resolvedAssemblies.ContainsKey(name))
                    resolvedAssemblies[name] = patchDefinition.Instance.GetType().ToString();
            }
        }

        // Check if any patched assemblies have been already resolved by the CLR
        // If there are any, they cannot be loaded by the preloader
        var patchedAssemblyNames =
            new
                HashSet<string>(assemblies.Where(kv => patchedAssemblies.Contains(kv.Key)).Select(kv => kv.Value.Name.Name),
                                StringComparer.InvariantCultureIgnoreCase);
        var earlyLoadAssemblies = resolvedAssemblies.Where(kv => patchedAssemblyNames.Contains(kv.Key)).ToList();

        if (earlyLoadAssemblies.Count != 0)
            Logger.Log(LogLevel.Warning, new StringBuilder()
                                         .AppendLine("The following assemblies have been loaded too early and will not be patched by preloader:")
                                         .AppendLine(string.Join(Environment.NewLine,
                                                                 earlyLoadAssemblies
                                                                     .Select(kv =>
                                                                                 $"* [{kv.Key}] (first loaded by [{kv.Value}])")
                                                                     .ToArray()))
                                         .AppendLine("Expect unexpected behavior and issues with plugins and patchers not being loaded.")
                                         .ToString());

        var dumpedAssemblyPaths = new Dictionary<string, string>();
        // Finally, load patched assemblies into memory
        if (ConfigDumpAssemblies.Value || ConfigLoadDumpedAssemblies.Value)
        {
            if (!Directory.Exists(PatcherContext.DumpedAssembliesPath))
                Directory.CreateDirectory(PatcherContext.DumpedAssembliesPath);

            foreach (var kv in assemblies)
            {
                var filename = kv.Key;
                var name = Path.GetFileNameWithoutExtension(filename);
                var ext = Path.GetExtension(filename);
                var assembly = kv.Value;

                if (!patchedAssemblies.Contains(filename))
                    continue;
                for (var i = 0;; i++)
                {
                    var postfix = i > 0 ? $"_{i}" : "";
                    var path = Path.Combine(PatcherContext.DumpedAssembliesPath, $"{name}{postfix}{ext}");
                    if (!Utility.TryOpenFileStream(path, FileMode.Create, out var fs))
                        continue;
                    assembly.Write(fs);
                    fs.Dispose();
                    dumpedAssemblyPaths[filename] = path;
                    break;
                }
            }
        }

        if (ConfigBreakBeforeLoadAssemblies.Value)
        {
            Logger.Log(LogLevel.Info,
                       $"BepInEx is about load the following assemblies:\n{string.Join("\n", patchedAssemblies.ToArray())}");
            Logger.Log(LogLevel.Info, $"The assemblies were dumped into {PatcherContext.DumpedAssembliesPath}");
            Logger.Log(LogLevel.Info, "Load any assemblies into the debugger, set breakpoints and continue execution.");
            Debugger.Break();
        }

        foreach (var kv in assemblies)
        {
            var filename = kv.Key;
            var assembly = kv.Value;

            // Note that since we only *load* assemblies, they shouldn't trigger dependency loading
            // Not loading all assemblies is very important not only because of memory reasons,
            // but because some games *rely* on that because of messed up internal dependencies.
            if (patchedAssemblies.Contains(filename))
            {
                Assembly loadedAssembly;

                if (ConfigLoadDumpedAssemblies.Value &&
                    dumpedAssemblyPaths.TryGetValue(filename, out var dumpedAssemblyPath))
                {
                    loadedAssembly = Assembly.LoadFrom(dumpedAssemblyPath);
                }
                else
                {
                    using var assemblyStream = new MemoryStream();
                    assembly.Write(assemblyStream);
                    loadedAssembly = assemblyLoader(assemblyStream.ToArray(),
                                                    PatcherContext.AvailableAssembliesPaths[filename]);
                }

                PatcherContext.LoadedAssemblies.Add(filename, loadedAssembly);

                Logger.Log(LogLevel.Debug, $"Loaded '{assembly.FullName}' into memory");
            }

            // Though we have to dispose of all assemblies regardless of them being patched or not
            assembly.Dispose();
        }

        AllAssembliesPatched?.Invoke();
    }
}
