using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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
    private static readonly string CurrentAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;

    /// <summary>
    ///     The context of this assembly patcher instance that is passed to all patcher plugins.
    /// </summary>
    public PatcherContext PatcherContext { get; } = new()
    {
        DumpedAssembliesPath = Utility.CombinePaths(Paths.BepInExRootPath, "DumpedAssemblies", Paths.ProcessName)
    };

    /// <summary>
    ///     A cloned version of <see cref="PatcherPlugins" /> to ensure that any foreach loops do not break when the collection
    ///     gets modified.
    /// </summary>
    private IEnumerable<BasePatcher> PatcherPluginsSafe => PatcherContext.PatcherPlugins.ToList();

    private ManualLogSource Logger { get; } = BepInEx.Logging.Logger.CreateLogSource("AssemblyPatcher");

    private static Regex allowedGuidRegex { get; } = new(@"^[a-zA-Z0-9\._\-]+$");

    /// <summary>
    ///     Performs work to dispose collection objects.
    /// </summary>
    public void Dispose()
    {
        foreach (var assembly in PatcherContext.AvailableAssemblies)
            assembly.Value.Dispose();

        PatcherContext.AvailableAssemblies.Clear();

        // Clear to allow GC collection.
        PatcherContext.PatcherPlugins.Clear();
    }

    private PatcherPluginMetadata ToPatcherPlugin(TypeDefinition type, string assemblyPath)
    {
        if (type.IsInterface || type.IsAbstract && !type.IsSealed)
            return null;

        try
        {
            if (!type.IsSubtypeOf(typeof(BasePatcher)))
                return null;
        }
        catch (AssemblyResolutionException)
        {
            // Can happen if this type inherits a type from an assembly that can't be found. Safe to assume it's not a plugin.
            return null;
        }

        var metadata = PatcherPluginInfoAttribute.FromCecilType(type);

        // Perform checks that will prevent the plugin from being loaded in ALL cases
        if (metadata == null)
        {
            Logger.Log(LogLevel.Warning, $"Skipping over type [{type.FullName}] as no metadata attribute is specified");
            return null;
        }

        if (string.IsNullOrEmpty(metadata.GUID) || !allowedGuidRegex.IsMatch(metadata.GUID))
        {
            Logger.Log(LogLevel.Warning,
                       $"Skipping type [{type.FullName}] because its GUID [{metadata.GUID}] is of an illegal format");
            return null;
        }

        if (metadata.Version == null)
        {
            Logger.Log(LogLevel.Warning, $"Skipping type [{type.FullName}] because its version is invalid");
            return null;
        }

        if (metadata.Name == null)
        {
            Logger.Log(LogLevel.Warning, $"Skipping type [{type.FullName}] because its name is null");
            return null;
        }

        return new PatcherPluginMetadata
        {
            TypeName = type.FullName
        };
    }

    private bool HasPatcherPlugins(AssemblyDefinition ass)
    {
        if (ass.MainModule.AssemblyReferences.All(r => r.Name != CurrentAssemblyName) &&
            ass.Name.Name != CurrentAssemblyName)
            return false;
        if (ass.MainModule.GetTypeReferences().All(r => r.FullName != typeof(BasePatcher).FullName))
            return false;

        return true;
    }

    /// <summary>
    ///     Adds all patchers from all managed assemblies specified in a directory.
    /// </summary>
    /// <param name="directory">Directory to search patcher DLLs from.</param>
    public void AddPatchersFromDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        var sortedPatchers = new List<PatchDefinition>();

        var patchers = TypeLoader.FindPluginTypes(directory, ToPatcherPlugin, HasPatcherPlugins);

        // TODO: Add dependency ordering and process attribute filtering

        foreach (var keyValuePair in patchers)
        {
            var assemblyPath = keyValuePair.Key;
            var patcherCollection = keyValuePair.Value;

            if (patcherCollection.Count == 0)
                continue;

            var ass = Assembly.LoadFrom(assemblyPath);

            foreach (var patcherPlugin in patcherCollection)
                try
                {
                    var type = ass.GetType(patcherPlugin.TypeName);

                    var instance = (BasePatcher) Activator.CreateInstance(type);
                    instance.Context = PatcherContext;

                    PatcherContext.PatcherPlugins.Add(instance);

                    var methods =
                        type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    foreach (var method in methods)
                    {
                        var targetAssemblies = MetadataHelper.GetAttributes<TargetAssemblyAttribute>(method);
                        var targetTypes = MetadataHelper.GetAttributes<TargetTypeAttribute>(method);

                        if (targetAssemblies.Length == 0 && targetTypes.Length == 0)
                            continue;

                        var parameters = method.GetParameters();

                        if (parameters.Length < 1 || parameters.Length > 2
                                                     // Next few lines ensure that the first parameter is AssemblyDefinition and does not have any
                                                     // target type attributes, and vice versa
                                                  || !(
                                                          parameters[0].ParameterType == typeof(AssemblyDefinition)
                                                       || parameters[0].ParameterType ==
                                                          typeof(AssemblyDefinition).MakeByRefType()
                                                       && targetTypes.Length == 0
                                                       || parameters[0].ParameterType == typeof(TypeDefinition)
                                                       && targetAssemblies.Length == 0
                                                      )
                                                  || parameters.Length == 2 &&
                                                     parameters[1].ParameterType != typeof(string)
                                                  || method.ReturnType != typeof(void) &&
                                                     method.ReturnType != typeof(bool)
                           )
                        {
                            Logger
                                .Log(LogLevel.Warning,
                                     $"Skipping method [{method.FullDescription()}] as it is not a valid patcher method");
                            continue;
                        }

                        void AddDefinition(PatchDefinition definition)
                        {
                            Logger.Log(LogLevel.Debug, $"Discovered patch [{definition.FullName}]");
                            sortedPatchers.Add(definition);
                        }

                        foreach (var targetAssembly in targetAssemblies)
                            AddDefinition(new PatchDefinition(targetAssembly, instance, method));
                        foreach (var targetType in targetTypes)
                            AddDefinition(new PatchDefinition(targetType, instance, method));
                    }
                }
                catch (Exception e)
                {
                    Logger.Log(LogLevel.Error,
                               $"Failed to load patchers from type [{patcherPlugin.TypeName}]: {(e is ReflectionTypeLoadException re ? TypeLoader.TypeLoadExceptionToString(re) : e.ToString())}");
                }

            var assName = ass.GetName();
            Logger.Log(patcherCollection.Any() ? LogLevel.Info : LogLevel.Debug,
                       $"Loaded {patcherCollection.Count} patcher type{(patcherCollection.Count == 1 ? "" : "s")} from [{assName.Name} {assName.Version}]");
        }

        PatcherContext.PatchDefinitions.AddRange(sortedPatchers);
    }


    /// <summary>
    ///     Adds all .dll assemblies in given directories to be patched and loaded by this patcher instance. Non-managed
    ///     assemblies
    ///     are skipped.
    /// </summary>
    /// <param name="directories">The directories to search.</param>
    public void LoadAssemblyDirectories(params string[] directories) =>
        LoadAssemblyDirectories(directories, new[] { "dll" });

    /// <summary>
    ///     Adds all assemblies in given directories to be patched and loaded by this patcher instance. Non-managed assemblies
    ///     are
    ///     skipped.
    /// </summary>
    /// <param name="directories">The directory to search.</param>
    /// <param name="assemblyExtensions">The file extensions to attempt to load.</param>
    public void LoadAssemblyDirectories(IEnumerable<string> directories, IEnumerable<string> assemblyExtensions)
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

            PatcherContext.AvailableAssemblies.Add(Path.GetFileName(assemblyPath), assembly);

            Logger.LogDebug($"Assembly loaded: {Path.GetFileName(assemblyPath)}");

            //if (UnityPatches.AssemblyLocations.ContainsKey(assembly.FullName))
            //{
            //	Logger.LogWarning($"Tried to load duplicate assembly {Path.GetFileName(assemblyPath)} from Managed folder! Skipping...");
            //	continue;
            //}

            //assemblies.Add(Path.GetFileName(assemblyPath), assembly);
            //UnityPatches.AssemblyLocations.Add(assembly.FullName, Path.GetFullPath(assemblyPath));
        }
    }

    /// <summary>
    ///     Attempts to load a managed assembly as an <see cref="AssemblyDefinition" />. Returns true if successful.
    /// </summary>
    /// <param name="path">The path of the assembly.</param>
    /// <param name="assembly">The loaded assembly. Null if not successful in loading.</param>
    public static bool TryLoadAssembly(string path, out AssemblyDefinition assembly)
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
    public void PatchAndLoad()
    {
        // First, create a copy of the assembly dictionary as the initializer can change them
        var assemblies =
            new Dictionary<string, AssemblyDefinition>(PatcherContext.AvailableAssemblies,
                                                       StringComparer.InvariantCultureIgnoreCase);

        // Next, initialize all the patchers
        foreach (var assemblyPatcher in PatcherPluginsSafe)
            try
            {
                assemblyPatcher.Initialize();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, $"Failed to run initializer of {assemblyPatcher.Info.GUID}: {ex}");
            }

        // Then, perform the actual patching

        var patchedAssemblies = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        var resolvedAssemblies = new Dictionary<string, string>();

        // TODO: Maybe instead reload the assembly and repatch with other valid patchers?
        var invalidAssemblies = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        Logger.Log(LogLevel.Message, $"Executing {PatcherContext.PatchDefinitions.Count} patch(es)");

        foreach (var patchDefinition in PatcherContext.PatchDefinitions.ToList())
        {
            var targetDll = patchDefinition.TargetAssembly?.TargetAssembly ??
                            patchDefinition.TargetType.TargetAssembly;

            var isAssemblyPatch = patchDefinition.TargetAssembly != null;

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
                    var arguments = new object[patchDefinition.MethodInfo.GetParameters().Length];

                    if (!isAssemblyPatch)
                    {
                        var targetType =
                            assembly.MainModule.Types.FirstOrDefault(x => x.FullName ==
                                                                          patchDefinition.TargetType.TargetType);

                        if (targetType == null)
                        {
                            Logger
                                .LogWarning($"Unable to find type [{patchDefinition.TargetType.TargetType}] defined in {patchDefinition.MethodInfo.Name}. Skipping patcher"); //TODO: Proper name
                            return false;
                        }

                        arguments[0] = targetType;
                    }
                    else
                    {
                        arguments[0] = assembly;
                    }

                    if (arguments.Length > 1)
                        arguments[1] = targetDll;

                    var result = patchDefinition.MethodInfo.Invoke(patchDefinition.Instance, arguments);

                    if (patchDefinition.MethodInfo.ReturnType == typeof(void)
                     || patchDefinition.MethodInfo.ReturnType == typeof(bool) && (bool) result)
                    {
                        if (isAssemblyPatch)
                        {
                            assembly = (AssemblyDefinition) arguments[0];
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
                    resolvedAssemblies[name] = patchDefinition.MethodInfo.DeclaringType.ToString();
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
                    loadedAssembly = Assembly.Load(assemblyStream.ToArray());
                }

                PatcherContext.LoadedAssemblies.Add(filename, loadedAssembly);

                Logger.Log(LogLevel.Debug, $"Loaded '{assembly.FullName}' into memory");
            }

            // Though we have to dispose of all assemblies regardless of them being patched or not
            assembly.Dispose();
        }

        // Finally, run all finalizers
        foreach (var assemblyPatcher in PatcherPluginsSafe)
            try
            {
                assemblyPatcher.Finalizer();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, $"Failed to run finalizer of {assemblyPatcher.Info.GUID}: {ex}");
            }
    }

    #region Config

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

    #endregion
}
