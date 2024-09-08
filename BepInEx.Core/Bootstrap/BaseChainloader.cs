using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using BepInEx.Logging;
using Mono.Cecil;

namespace BepInEx.Bootstrap;

public abstract class BaseChainloader<TPlugin>
{
    protected static readonly string CurrentAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;
    protected static readonly Version CurrentAssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;

    private static Regex allowedGuidRegex { get; } = new(@"^[a-zA-Z0-9\._\-]+$");

    internal BaseChainloader() { }

    /// <summary>
    ///     Analyzes the given type definition and attempts to convert it to a valid <see cref="PluginInfo" />
    /// </summary>
    /// <param name="type">Type definition to analyze.</param>
    /// <param name="loadContext">The load context of the plugin</param>
    /// <param name="assemblyLocation">The filepath of the assembly, to keep as metadata.</param>
    /// <returns>If the type represent a valid plugin, returns a <see cref="PluginInfo" /> instance. Otherwise, return null.</returns>
    public static PluginInfo ToPluginInfo<T>(TypeDefinition type, IPluginLoadContext loadContext, string assemblyLocation)
    {
        if (type.IsInterface || type.IsAbstract)
            return null;

        try
        {
            if (!type.IsSubtypeOf(typeof(T)))
                return null;
        }
        catch (AssemblyResolutionException)
        {
            // Can happen if this type inherits a type from an assembly that can't be found. Safe to assume it's not a plugin.
            return null;
        }

        if (type.IsSubtypeOf(typeof(BasePluginProvider)))
            return GetPluginInfo(type, BepInPluginProvider.FromCecilType(type), loadContext, assemblyLocation);
        return GetPluginInfo(type, BepInPlugin.FromCecilType(type), loadContext, assemblyLocation);
    }

    private static PluginInfo GetPluginInfo(TypeDefinition type, BepInPlugin metadata, IPluginLoadContext loadContext, string assemblyLocation)
    {
        // Perform checks that will prevent the plugin from being loaded in ALL cases
        if (metadata == null)
        {
            Logger.Log(LogLevel.Warning, $"Skipping over type [{type.FullName}] as no metadata attribute is specified");
            return null;
        }

        if (string.IsNullOrEmpty(metadata.GUID) || !allowedGuidRegex.IsMatch(metadata.GUID))
        {
            Logger.Log(LogLevel.Warning,
                       $"Skipping type [{type.FullName}] because its GUID [{metadata.GUID}] is of an illegal format.");
            return null;
        }

        if (metadata.Version == null)
        {
            Logger.Log(LogLevel.Warning, $"Skipping type [{type.FullName}] because its version is invalid.");
            return null;
        }

        if (metadata.Name == null)
        {
            Logger.Log(LogLevel.Warning, $"Skipping type [{type.FullName}] because its name is null.");
            return null;
        }

        var filters = BepInProcess.FromCecilType(type);
        var dependencies = BepInDependency.FromCecilType(type);
        var incompatibilities = BepInIncompatibility.FromCecilType(type);

        var bepinVersion =
            type.Module.AssemblyReferences.FirstOrDefault(reference => reference.Name == "BepInEx.Core")?.Version ??
            new Version();

        return new PluginInfo
        {
            Metadata = metadata,
            Processes = filters,
            Dependencies = dependencies,
            Incompatibilities = incompatibilities,
            TypeName = type.FullName,
            TargettedBepInExVersion = bepinVersion,
            _location = assemblyLocation,
            LoadContext = loadContext
        };
    }

    protected static bool HasBepinPluginType<T>(AssemblyDefinition ass)
    {
        if (ass.MainModule.AssemblyReferences.All(r => r.Name != CurrentAssemblyName))
            return false;
        if (ass.MainModule.GetTypeReferences().All(r => r.FullName != typeof(T).FullName))
            return false;

        return true;
    }

    protected static bool PluginTargetsWrongBepin(PluginInfo pluginInfo)
    {
        var pluginTarget = pluginInfo.TargettedBepInExVersion;
        // X.X.X.x - compare normally. x.x.x.X - nightly build number, ignore
        if (pluginTarget.Major != CurrentAssemblyVersion.Major) return true;
        if (pluginTarget.Minor > CurrentAssemblyVersion.Minor) return true;
        if (pluginTarget.Minor < CurrentAssemblyVersion.Minor) return false;
        return pluginTarget.Build > CurrentAssemblyVersion.Build;
    }

    #region Contract

    protected virtual string ConsoleTitle => $"BepInEx {Paths.BepInExVersion} - {Paths.ProcessName}";

    private bool _initialized;

    /// <summary>
    /// The current chainloader instance
    /// </summary>
    public static BaseChainloader<TPlugin> Instance { get; protected set; }
    
    /// <summary>
    ///     List of all <see cref="PluginInfo" /> instances of all plugins loaded via the chainloader.
    ///     If a plugin fails to load, it is removed. This list only contains the plugins that were actually
    ///     loaded and have their dependencies satisfied.
    /// </summary>
    public Dictionary<string, PluginInfo> Plugins { get; } = new();

    /// <summary>
    ///     List of all <see cref="PluginInfo" /> instances of all providers loaded via the chainloader.
    ///     If a provider fails to load, it is removed. This list only contains the providers that were actually
    ///     loaded and have their dependencies satisfied.
    /// </summary>
    public Dictionary<string, PluginInfo> Providers { get; } = new();
    
    /// <summary>
    ///     List of all <see cref="IPluginLoadContext" /> objects received from plugin providers.
    ///     Value holds info of the originating provider. These contexts are used by chainloader to
    ///     load plugins. Plugins that are loaded successfully are added to <see cref="Plugins" />.
    /// </summary>
    internal Dictionary<CachedPluginLoadContext, PluginInfo> LoadContexts { get; } = new();
    
    /// <summary>
    ///     Collection of error chainloader messages that occured during plugin loading.
    ///     Contains information about what certain plugins were not loaded.
    /// </summary>
    public List<string> DependencyErrors { get; } = new();

    /// <summary>
    ///     Occurs after a plugin provider is loaded.
    /// </summary>
    public event Action<PluginInfo> ProviderLoaded;
    
    /// <summary>
    ///     Occurs after all plugins providers are loaded.
    /// </summary>
    public event Action FinishedProviders;
    
    /// <summary>
    ///     Occurs after a plugin is loaded.
    /// </summary>
    public event Action<PluginInfo> PluginLoaded;

    /// <summary>
    ///     Occurs after all plugins are loaded.
    /// </summary>
    public event Action Finished;

    public virtual void Initialize(string gameExePath = null)
    {
        if (_initialized)
            throw new InvalidOperationException("Chainloader cannot be initialized multiple times");

        // Set vitals
        if (gameExePath != null)
            // Checking for null allows a more advanced initialization workflow, where the Paths class has been initialized before calling Chainloader.Initialize
            // This is used by Preloader to use environment variables, for example
            Paths.SetExecutablePath(gameExePath);

        InitializeLoggers();

        if (!Directory.Exists(Paths.PluginProviderPath))
            Directory.CreateDirectory(Paths.PluginProviderPath);

        if (!Directory.Exists(Paths.PluginPath))
            Directory.CreateDirectory(Paths.PluginPath);

        _initialized = true;

        Logger.Log(LogLevel.Message, "Chainloader initialized");
    }

    protected virtual void InitializeLoggers()
    {
        if (ConsoleManager.ConsoleEnabled && !ConsoleManager.ConsoleActive)
            ConsoleManager.CreateConsole();

        if (ConsoleManager.ConsoleActive)
        {
            if (!Logger.Listeners.Any(x => x is ConsoleLogListener))
                Logger.Listeners.Add(new ConsoleLogListener());

            ConsoleManager.SetConsoleTitle(ConsoleTitle);
        }

        if (ConfigDiskLogging.Value)
            Logger.Listeners.Add(new DiskLogListener("LogOutput.log", ConfigDiskLoggingDisplayedLevel.Value,
                                                     ConfigDiskAppend.Value, ConfigDiskLoggingInstantFlushing.Value,
                                                     ConfigDiskLoggingFileLimit.Value));

        if (!TraceLogSource.IsListening)
            Logger.Sources.Add(TraceLogSource.CreateSource());

        if (!Logger.Sources.Any(x => x is HarmonyLogSource))
            Logger.Sources.Add(new HarmonyLogSource());
    }

    /// <summary>
    /// Discovers all plugins in the plugin directory without loading them.
    /// </summary>
    /// <remarks>
    /// This is useful for discovering BepInEx plugin metadata.
    /// </remarks>
    /// <param name="path">Path from which to search the plugins.</param>
    /// <param name="cacheName">Cache name to use. If null, results are not cached.</param>
    /// <returns>List of discovered plugins and their metadata.</returns>
    protected List<PluginInfo> DiscoverPluginProvidersFrom(string path, string cacheName = "chainloader_meta")
    {
        var pluginsToLoad =
            TypeLoader.FindPluginTypes(path, ToPluginInfo<BasePluginProvider>, HasBepinPluginType<BasePluginProvider>, cacheName);
        return pluginsToLoad.SelectMany(p => p.Value).ToList();
    }

    /// <summary>
    /// Preprocess the plugins and modify the load order.
    /// </summary>
    /// <remarks>Some plugins may be skipped if they cannot be loaded (wrong metadata, etc).</remarks>
    /// <param name="plugins">Plugins to process.</param>
    /// <returns>List of plugins to load in the correct load order.</returns>
    protected virtual List<PluginInfo> ModifyLoadOrder(IList<PluginInfo> plugins, Dictionary<string, PluginInfo> existingPlugins)
    {
        // We use a sorted dictionary to ensure consistent load order
        var dependencyDict =
            new SortedDictionary<string, IEnumerable<string>>(StringComparer.InvariantCultureIgnoreCase);
        var pluginsByGuid = new Dictionary<string, PluginInfo>();

        foreach (var pluginInfoGroup in plugins.GroupBy(info => info.Metadata.GUID))
        {
            if (existingPlugins.TryGetValue(pluginInfoGroup.Key, out var loadedPlugin))
            {
                Logger.Log(LogLevel.Warning,
                           $"Skipping [{pluginInfoGroup.Key}] because a plugin with a similar GUID ([{loadedPlugin}]) has been already loaded.");
                continue;
            }

            PluginInfo loadedVersion = null;
            foreach (var pluginInfo in pluginInfoGroup.OrderByDescending(x => x.Metadata.Version))
            {
                if (loadedVersion != null)
                {
                    Logger.Log(LogLevel.Warning,
                               $"Skipping [{pluginInfo}] because a newer version exists ({loadedVersion})");
                    continue;
                }

                // Perform checks that will prevent loading plugins in this run
                var filters = pluginInfo.Processes.ToList();
                var invalidProcessName = filters.Count != 0 &&
                                         filters.All(x => !string.Equals(x.ProcessName.Replace(".exe", ""),
                                                                         Paths.ProcessName,
                                                                         StringComparison
                                                                             .InvariantCultureIgnoreCase));

                if (invalidProcessName)
                {
                    Logger.Log(LogLevel.Warning,
                               $"Skipping [{pluginInfo}] because of process filters ({string.Join(", ", pluginInfo.Processes.Select(p => p.ProcessName).ToArray())})");
                    continue;
                }

                loadedVersion = pluginInfo;
                dependencyDict[pluginInfo.Metadata.GUID] = pluginInfo.Dependencies.Select(d => d.DependencyGUID);
                pluginsByGuid[pluginInfo.Metadata.GUID] = pluginInfo;
            }
        }

        foreach (var pluginInfo in pluginsByGuid.Values.ToList())
            if (pluginInfo.Incompatibilities.Any(incompatibility =>
                                                     pluginsByGuid.ContainsKey(incompatibility.IncompatibilityGUID)
                                                  || existingPlugins.ContainsKey(incompatibility.IncompatibilityGUID))
               )
            {
                pluginsByGuid.Remove(pluginInfo.Metadata.GUID);
                dependencyDict.Remove(pluginInfo.Metadata.GUID);

                var incompatiblePluginsNew = pluginInfo.Incompatibilities.Select(x => x.IncompatibilityGUID)
                                                       .Where(x => pluginsByGuid.ContainsKey(x));
                var incompatiblePluginsExisting = pluginInfo.Incompatibilities.Select(x => x.IncompatibilityGUID)
                                                            .Where(x => existingPlugins.ContainsKey(x));
                var incompatiblePlugins = incompatiblePluginsNew.Concat(incompatiblePluginsExisting).ToArray();
                var message =
                    $@"Could not load [{pluginInfo}] because it is incompatible with: {string.Join(", ", incompatiblePlugins)}";
                DependencyErrors.Add(message);
                Logger.Log(LogLevel.Error, message);
            }
            else if (PluginTargetsWrongBepin(pluginInfo))
            {
                var message =
                    $@"Plugin [{pluginInfo}] targets a wrong version of BepInEx ({pluginInfo.TargettedBepInExVersion}) and might not work until you update";
                DependencyErrors.Add(message);
                Logger.Log(LogLevel.Warning, message);
            }

        // We don't add already loaded plugins to the dependency graph as they are already loaded

        var emptyDependencies = new string[0];

        // Sort plugins by their dependencies.
        // Give missing dependencies no dependencies of its own, which will cause missing plugins to be first in the resulting list.
        var sortedPlugins = Utility.TopologicalSort(dependencyDict.Keys,
                                                    x =>
                                                        dependencyDict.TryGetValue(x, out var deps)
                                                            ? deps
                                                            : emptyDependencies).ToList();

        return sortedPlugins.Where(pluginsByGuid.ContainsKey).Select(x => pluginsByGuid[x]).ToList();
    }

    /// <summary>
    /// Run the chainloader and load all plugins from all providers.
    /// </summary>
    public virtual void Execute()
    {
        try
        {
            var builtinProviders = DiscoverPluginProvidersFrom(Paths.BepInExAssemblyDirectory);
            var providers = DiscoverPluginProvidersFrom(Paths.PluginProviderPath);
            Logger.Log(LogLevel.Info, $"{providers.Count + builtinProviders.Count} " +
                                      $"plugin provider{(providers.Count + builtinProviders.Count == 1 ? "" : "s")} to load");
            
            LoadPlugins(builtinProviders, provider: true);
            LoadPlugins(providers, provider: true);
            FinishedProviders?.Invoke();
            
            var plugins = TypeLoader.GetPluginsFromLoadContexts(LoadContexts.Keys.Cast<IPluginLoadContext>(), ToPluginInfo<TPlugin>, HasBepinPluginType<BepInPlugin>, "chainloader");
            Logger.Log(LogLevel.Info, $"{plugins.Count} plugin{(plugins.Count == 1 ? "" : "s")} to load");
            LoadPlugins(plugins, provider: false);
            Finished?.Invoke();
        }
        catch (Exception ex)
        {
            try
            {
                ConsoleManager.CreateConsole();
            }
            catch { }

            Logger.Log(LogLevel.Error, $"Error occurred loading plugins: {ex}");
        }

        Logger.Log(LogLevel.Message, "Chainloader startup complete");
    }

    [Obsolete("Use a plugin provider instead", true)]
    public IList<PluginInfo> LoadPlugins(params string[] pluginsPaths)
    {
        throw new NotSupportedException("Use a plugin provider instead");
    }
    
    private void LoadPlugins(List<PluginInfo> plugins, bool provider)
    {
        var existingPluginInfos = provider ? Providers : Plugins;
        var sortedPluginInfos = ModifyLoadOrder(plugins, existingPluginInfos);
        
        var invalidPlugins = new HashSet<string>();
        var processedPlugins = new Dictionary<string, SemanticVersioning.Version>();
        var loadedAssemblies = new Dictionary<string, Assembly>();

        foreach (var pluginInfo in sortedPluginInfos)
        {
            if (!CheckDependencies(pluginInfo, processedPlugins, invalidPlugins, existingPluginInfos))
            {
                continue;
            }

            try
            {
                Logger.Log(LogLevel.Info, $"Loading [{pluginInfo}]");

                Assembly ass = null;
                if (provider && !loadedAssemblies.TryGetValue(pluginInfo._location, out ass))
                {
                    ass = Assembly.LoadFrom(pluginInfo._location);
                    loadedAssemblies[pluginInfo._location] = ass;
                }
                else if (!provider && !loadedAssemblies.TryGetValue(pluginInfo.LoadContext.AssemblyIdentifier, out ass))
                {
                    var symbols = pluginInfo.LoadContext.GetAssemblySymbolsData();
                    ass = symbols != null ? Assembly.Load(pluginInfo.LoadContext.GetAssemblyData(), symbols)
                                          : Assembly.Load(pluginInfo.LoadContext.GetAssemblyData());
                    loadedAssemblies[pluginInfo.LoadContext.AssemblyIdentifier] = ass;
                }

                existingPluginInfos[pluginInfo.Metadata.GUID] = pluginInfo;
                TryRunModuleCtor(pluginInfo, ass);

                if (provider)
                {
                    var type = ass.GetType(pluginInfo.TypeName);
                    pluginInfo.Instance = Activator.CreateInstance(type);
                    AppDomain.CurrentDomain.AssemblyResolve += (_, args) => ((BasePluginProvider)pluginInfo.Instance).ResolveAssembly(args.Name);
                    var pluginLoadContexts = ((BasePluginProvider)pluginInfo.Instance).GetPlugins();
                    foreach (IPluginLoadContext context in pluginLoadContexts)
                    {
                        var cachedContext = new CachedPluginLoadContext(context);
                        LoadContexts[cachedContext] = pluginInfo;
                    }
                    ProviderLoaded?.Invoke(pluginInfo);
                }
                else
                {
                    pluginInfo.Source = LoadContexts[(CachedPluginLoadContext)pluginInfo.LoadContext];
                    pluginInfo.Instance = LoadPlugin(pluginInfo, ass);
                    PluginLoaded?.Invoke(pluginInfo);
                    foreach (CachedPluginLoadContext context in LoadContexts.Keys)
                        context.Dispose();
                }
            }
            catch (Exception ex)
            {
                invalidPlugins.Add(pluginInfo.Metadata.GUID);
                existingPluginInfos.Remove(pluginInfo.Metadata.GUID);

                Logger.Log(LogLevel.Error,
                           $"Error loading [{pluginInfo}]: {(ex is ReflectionTypeLoadException re ? TypeLoader.TypeLoadExceptionToString(re) : ex.ToString())}");
            }
        }
    }

    private bool CheckDependencies<T>(T plugin, Dictionary<string, SemanticVersioning.Version> processedPlugins, HashSet<string> invalidPlugins, Dictionary<string, T> existingPlugins)
        where T : PluginInfo
    {
        var dependsOnInvalidPlugin = false;
        var missingDependencies = new List<BepInDependency>();
        foreach (var dependency in plugin.Dependencies)
        {
            static bool IsHardDependency(BepInDependency dep) =>
                (dep.Flags & BepInDependency.DependencyFlags.HardDependency) != 0;

            // If the dependency wasn't already processed, it's missing altogether
            var dependencyExists =
                processedPlugins.TryGetValue(dependency.DependencyGUID, out var pluginVersion);
            // Alternatively, if the dependency hasn't been loaded before, it's missing too
            if (!dependencyExists)
            {
                dependencyExists = existingPlugins.TryGetValue(dependency.DependencyGUID, out var pluginInfo);
                pluginVersion = pluginInfo?.Metadata.Version;
            }

            if (!dependencyExists || dependency.VersionRange != null &&
                !dependency.VersionRange.IsSatisfied(pluginVersion))
            {
                // If the dependency is hard, collect it into a list to show
                if (IsHardDependency(dependency))
                    missingDependencies.Add(dependency);
                continue;
            }

            // If the dependency is a hard and is invalid (e.g. has missing dependencies), report that to the user
            if (invalidPlugins.Contains(dependency.DependencyGUID) && IsHardDependency(dependency))
            {
                dependsOnInvalidPlugin = true;
                break;
            }
        }

        processedPlugins.Add(plugin.Metadata.GUID, plugin.Metadata.Version);

        if (dependsOnInvalidPlugin)
        {
            var message =
                $"Skipping [{plugin}] because it has a dependency that was not loaded. See previous errors for details.";
            DependencyErrors.Add(message);
            Logger.Log(LogLevel.Warning, message);
            return false;
        }

        if (missingDependencies.Count != 0)
        {
            var message = $@"Could not load [{plugin}] because it has missing dependencies: {
                string.Join(", ", missingDependencies.Select(s => s.VersionRange == null ? s.DependencyGUID : $"{s.DependencyGUID} ({s.VersionRange})").ToArray())
            }";
            DependencyErrors.Add(message);
            Logger.Log(LogLevel.Error, message);

            invalidPlugins.Add(plugin.Metadata.GUID);
            return false;
        }

        return true;
    }

    private static void TryRunModuleCtor(PluginInfo plugin, Assembly assembly)
    {
        try
        {
            RuntimeHelpers.RunModuleConstructor(assembly.GetType(plugin.TypeName).Module.ModuleHandle);
        }
        catch (Exception e)
        {
            Logger.Log(LogLevel.Warning,
                       $"Couldn't run Module constructor for {assembly.FullName}::{plugin.TypeName}: {e}");
        }
    }

    public abstract TPlugin LoadPlugin(PluginInfo pluginInfo, Assembly pluginAssembly);

    #endregion

    #region Config

    private static readonly ConfigEntry<bool> ConfigDiskAppend = ConfigFile.CoreConfig.Bind(
     "Logging.Disk", "AppendLog",
     false,
     "Appends to the log file instead of overwriting, on game startup.");

    private static readonly ConfigEntry<bool> ConfigDiskLogging = ConfigFile.CoreConfig.Bind(
     "Logging.Disk", "Enabled",
     true,
     "Enables writing log messages to disk.");

    private static readonly ConfigEntry<LogLevel> ConfigDiskLoggingDisplayedLevel = ConfigFile.CoreConfig.Bind(
     "Logging.Disk", "LogLevels",
     LogLevel.Fatal | LogLevel.Error | LogLevel.Warning | LogLevel.Message | LogLevel.Info,
     "Only displays the specified log levels in the disk log output.");

    private static readonly ConfigEntry<bool> ConfigDiskLoggingInstantFlushing = ConfigFile.CoreConfig.Bind(
     "Logging.Disk", "InstantFlushing",
     false,
     new StringBuilder()
         .AppendLine("If true, instantly writes any received log entries to disk.")
         .AppendLine("This incurs a major performance hit if a lot of log messages are being written, however it is really useful for debugging crashes.")
         .ToString());

    private static readonly ConfigEntry<int> ConfigDiskLoggingFileLimit = ConfigFile.CoreConfig.Bind(
     "Logging.Disk", "ConcurrentFileLimit",
     5,
     new StringBuilder()
         .AppendLine("The maximum amount of concurrent log files that will be written to disk.")
         .AppendLine("As one log file is used per open game instance, you may find it necessary to increase this limit when debugging multiple instances at the same time.")
         .ToString());

    #endregion
}
