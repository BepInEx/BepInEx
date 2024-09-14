using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Core.Bootstrap;
using BepInEx.Logging;
using Mono.Cecil;

namespace BepInEx;

/// <summary>
///     The base type of any phases of a system to manage the loading of <see cref="Plugin"/> obtained from
///     multiple <see cref="PluginProvider"/>
/// </summary>
/// <typeparam name="TProvider">The provider type used to obtains the <see cref="IPluginLoadContext"/> of the plugins</typeparam>
/// <typeparam name="TPlugin">The plugin type provided by providers</typeparam>
public abstract class LoadingSystem<TProvider, TPlugin>
    where TProvider : PluginProvider
    where TPlugin : Plugin
{
    /// <summary>
    ///     The assembly name of this loading system
    /// </summary>
    protected readonly string CurrentAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;

    /// <summary>
    ///     The assembly version of this loading system
    /// </summary>
    protected readonly Version CurrentAssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;

    /// <summary>
    ///     The name a plugin should be referred to in the logs
    /// </summary>
    protected abstract string PluginLogName { get; }
    
    /// <summary>
    ///     The name of the assembly load cache of this loading system
    /// </summary>
    protected abstract string LoadCacheName { get; }

    private static Regex AllowedGuidRegex => new(@"^[a-zA-Z0-9\._\-]+$");

    internal LoadingSystem() {}
    
    /// <summary>
    ///     Analyzes the given type definition and attempts to convert it to a valid <see cref="PluginInfo" />
    /// </summary>
    /// <param name="type">Type definition to analyze.</param>
    /// <param name="loadContext">The load context of the plugin</param>
    /// <param name="assemblyLocation">The filepath of the assembly, to keep as metadata.</param>
    /// <returns>If the type represent a valid plugin, returns a <see cref="PluginInfo" /> instance. Otherwise, return null.</returns>
    protected static PluginInfo ToPluginInfo<T>(TypeDefinition type, IPluginLoadContext loadContext, string assemblyLocation)
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

        return GetPluginInfo(type, BepInMetadataAttribute.FromCecilType(type), loadContext, assemblyLocation);
    }

    private static PluginInfo GetPluginInfo(TypeDefinition type, BepInMetadataAttribute metadata, IPluginLoadContext loadContext, string assemblyLocation)
    {
        // Perform checks that will prevent the plugin from being loaded in ALL cases
        if (metadata == null)
        {
            Logger.Log(LogLevel.Warning, $"Skipping over type [{type.FullName}] as no metadata attribute is specified");
            return null;
        }

        if (string.IsNullOrEmpty(metadata.Guid) || !AllowedGuidRegex.IsMatch(metadata.Guid))
        {
            Logger.Log(LogLevel.Warning,
                       $"Skipping type [{type.FullName}] because its GUID [{metadata.Guid}] is of an illegal format.");
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
            TargetedBepInExVersion = bepinVersion,
            Location = assemblyLocation,
            LoadContext = loadContext
        };
    }

    /// <summary>
    ///     Determines if an assembly definition contains plugins of interest
    /// </summary>
    /// <param name="ass">The assembly definition to check for plugins</param>
    /// <typeparam name="T">The types of plugins</typeparam>
    /// <returns>Whether a plugin was found in the assembly</returns>
    protected bool HasPluginType<T>(AssemblyDefinition ass)
    {
        if (ass.MainModule.AssemblyReferences.All(r => r.Name != CurrentAssemblyName)
            && ass.Name.Name != CurrentAssemblyName)
            return false;
        if (ass.MainModule.GetTypeReferences().All(r => r.FullName != typeof(T).FullName))
            return false;

        return true;
    }

    private bool PluginTargetsWrongBepInExVersion(PluginInfo pluginInfo)
    {
        var pluginTarget = pluginInfo.TargetedBepInExVersion;
        // X.X.X.x - compare normally. x.x.x.X - nightly build number, ignore
        if (pluginTarget.Major != CurrentAssemblyVersion.Major) return true;
        if (pluginTarget.Minor > CurrentAssemblyVersion.Minor) return true;
        if (pluginTarget.Minor < CurrentAssemblyVersion.Minor) return false;
        return pluginTarget.Build > CurrentAssemblyVersion.Build;
    }

    /// <summary>
    ///     The title of the console
    /// </summary>
    protected virtual string ConsoleTitle => $"BepInEx {Utility.BepInExVersion} - {Paths.ProcessName}";

    /// <summary>
    ///     Whether the loading system was initialised
    /// </summary>
    protected bool Initialized { get; set; }
    
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
    public static event Action<PluginLoadEventArgs> ProviderLoaded;
    
    /// <summary>
    ///     Occurs after all plugins providers are loaded.
    /// </summary>
    public static event Action AllProvidersLoaded;
    
    /// <summary>
    ///     Occurs after a plugin is loaded.
    /// </summary>
    public static event Action<PluginLoadEventArgs> PluginLoaded;

    /// <summary>
    ///     Occurs after all plugins are loaded.
    /// </summary>
    public static event Action AllPluginsLoaded;

    /// <summary>
    /// Discovers all plugins in the plugin directory without loading them.
    /// </summary>
    /// <remarks>
    /// This is useful for discovering BepInEx plugin metadata.
    /// </remarks>
    /// <param name="path">Path from which to search the plugins.</param>
    /// <param name="cacheName">Cache name to use. If null, results are not cached.</param>
    /// <returns>List of discovered plugins and their metadata.</returns>
    private List<PluginInfo> DiscoverPluginProvidersFrom(string path, string cacheName)
    {
        var pluginsToLoad =
            TypeLoader.FindPluginTypes(path, ToPluginInfo<TProvider>, HasPluginType<TProvider>, cacheName);
        return pluginsToLoad.SelectMany(p => p.Value).ToList();
    }

    /// <summary>
    /// Preprocess the plugins and modify the load order.
    /// </summary>
    /// <remarks>Some plugins may be skipped if they cannot be loaded (wrong metadata, etc).</remarks>
    /// <param name="plugins">Plugins to process.</param>
    /// <param name="existingPlugins">The list of plugins that were already loaded by their GUID</param>
    /// <returns>List of plugins to load in the correct load order.</returns>
    private List<PluginInfo> ModifyLoadOrder(IList<PluginInfo> plugins, Dictionary<string, PluginInfo> existingPlugins)
    {
        // We use a sorted dictionary to ensure consistent load order
        var dependencyDict =
            new SortedDictionary<string, IEnumerable<string>>(StringComparer.InvariantCultureIgnoreCase);
        var pluginsByGuid = new Dictionary<string, PluginInfo>();

        foreach (var pluginInfoGroup in plugins.GroupBy(info => info.Metadata.Guid))
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
                dependencyDict[pluginInfo.Metadata.Guid] = pluginInfo.Dependencies.Select(d => d.DependencyGuid);
                pluginsByGuid[pluginInfo.Metadata.Guid] = pluginInfo;
            }
        }

        foreach (var pluginInfo in pluginsByGuid.Values.ToList())
            if (pluginInfo.Incompatibilities.Any(incompatibility =>
                                                     pluginsByGuid.ContainsKey(incompatibility.IncompatibilityGuid)
                                                  || existingPlugins.ContainsKey(incompatibility.IncompatibilityGuid))
               )
            {
                pluginsByGuid.Remove(pluginInfo.Metadata.Guid);
                dependencyDict.Remove(pluginInfo.Metadata.Guid);

                var incompatiblePluginsNew = pluginInfo.Incompatibilities.Select(x => x.IncompatibilityGuid)
                                                       .Where(x => pluginsByGuid.ContainsKey(x));
                var incompatiblePluginsExisting = pluginInfo.Incompatibilities.Select(x => x.IncompatibilityGuid)
                                                            .Where(x => existingPlugins.ContainsKey(x));
                var incompatiblePlugins = incompatiblePluginsNew.Concat(incompatiblePluginsExisting).ToArray();
                var message =
                    $@"Could not load [{pluginInfo}] because it is incompatible with: {string.Join(", ", incompatiblePlugins)}";
                DependencyErrors.Add(message);
                Logger.Log(LogLevel.Error, message);
            }
            else if (PluginTargetsWrongBepInExVersion(pluginInfo))
            {
                var message =
                    $@"Plugin [{pluginInfo}] targets a wrong version of BepInEx ({pluginInfo.TargetedBepInExVersion}) and might not work until you update";
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
    ///     Load all plugins or providers of plugins with dependency resolution
    /// </summary>
    /// <param name="plugins">A list of all the plugins to load</param>
    /// <param name="provider">Whether to load the providers or the plugins discovered by providers</param>
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
                if (provider && !loadedAssemblies.TryGetValue(pluginInfo.Location, out ass))
                {
                    ass = Assembly.LoadFrom(pluginInfo.Location);
                    loadedAssemblies[pluginInfo.Location] = ass;
                }
                else if (!provider && !loadedAssemblies.TryGetValue(pluginInfo.LoadContext.AssemblyIdentifier, out ass))
                {
                    var symbols = pluginInfo.LoadContext.GetAssemblySymbolsData();
                    ass = symbols != null ? Assembly.Load(pluginInfo.LoadContext.GetAssemblyData(), symbols)
                                          : Assembly.Load(pluginInfo.LoadContext.GetAssemblyData());
                    loadedAssemblies[pluginInfo.LoadContext.AssemblyIdentifier] = ass;
                }

                existingPluginInfos[pluginInfo.Metadata.Guid] = pluginInfo;
                TryRunModuleCtor(pluginInfo, ass);

                if (provider)
                {
                    var type = ass.GetType(pluginInfo.TypeName);
                    pluginInfo.Instance = Activator.CreateInstance(type);
                    AppDomain.CurrentDomain.AssemblyResolve += (_, args) => ((TProvider)pluginInfo.Instance).ResolveAssembly(args.Name);
                    var pluginLoadContexts = ((TProvider)pluginInfo.Instance).GetLoadContexts();
                    foreach (IPluginLoadContext context in pluginLoadContexts)
                    {
                        var cachedContext = new CachedPluginLoadContext(context);
                        LoadContexts[cachedContext] = pluginInfo;
                    }
                    ProviderLoaded?.Invoke(new(pluginInfo, ass, (Plugin)pluginInfo.Instance));
                }
                else
                {
                    pluginInfo.Source = LoadContexts[(CachedPluginLoadContext)pluginInfo.LoadContext];
                    pluginInfo.Instance = LoadPlugin(pluginInfo, ass);
                    PluginLoaded?.Invoke(new(pluginInfo, ass, (Plugin)pluginInfo.Instance));
                    foreach (CachedPluginLoadContext context in LoadContexts.Keys)
                        context.Dispose();
                }
            }
            catch (Exception ex)
            {
                invalidPlugins.Add(pluginInfo.Metadata.Guid);
                existingPluginInfos.Remove(pluginInfo.Metadata.Guid);

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
                processedPlugins.TryGetValue(dependency.DependencyGuid, out var pluginVersion);
            // Alternatively, if the dependency hasn't been loaded before, it's missing too
            if (!dependencyExists)
            {
                dependencyExists = existingPlugins.TryGetValue(dependency.DependencyGuid, out var pluginInfo);
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
            if (invalidPlugins.Contains(dependency.DependencyGuid) && IsHardDependency(dependency))
            {
                dependsOnInvalidPlugin = true;
                break;
            }
        }

        processedPlugins.Add(plugin.Metadata.Guid, plugin.Metadata.Version);

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
                string.Join(", ", missingDependencies.Select(s => s.VersionRange == null ? s.DependencyGuid : $"{s.DependencyGuid} ({s.VersionRange})").ToArray())
            }";
            DependencyErrors.Add(message);
            Logger.Log(LogLevel.Error, message);

            invalidPlugins.Add(plugin.Metadata.Guid);
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

    /// <summary>
    ///     Load all <typeparamref name="TPlugin"/> from all <typeparamref name="TProvider"/>
    /// </summary>
    internal virtual void LoadFromProviders()
    {
        try
        {
            var builtinProviders = DiscoverPluginProvidersFrom(Paths.BepInExAssemblyDirectory, $"{LoadCacheName}_providers");
            var providers = DiscoverPluginProvidersFrom(Paths.PluginProviderPath, $"{LoadCacheName}_providers");
            Logger.Log(LogLevel.Info, $"{providers.Count + builtinProviders.Count} " +
                                      $"{PluginLogName} provider{(providers.Count + builtinProviders.Count == 1 ? "" : "s")} to load");
            
            LoadPlugins(builtinProviders, provider: true);
            LoadPlugins(providers, provider: true);
            AllProvidersLoaded?.Invoke();
            var plugins = TypeLoader.GetPluginsFromLoadContexts(LoadContexts.Keys.Cast<IPluginLoadContext>(),
                                                                ToPluginInfo<TPlugin>,
                                                                HasPluginType<TPlugin>, LoadCacheName);
            Logger.Log(LogLevel.Info, $"{plugins.Count} {PluginLogName}{(plugins.Count == 1 ? "" : "s")} to load");
            LoadPlugins(plugins, provider: false);
            AllPluginsLoaded?.Invoke();
        }
        catch (Exception ex)
        {
            try
            {
                ConsoleManager.CreateConsole();
            }
            catch { }

            Logger.Log(LogLevel.Error, $"Error occurred loading {PluginLogName}s: {ex}");
        }
    }

    /// <summary>
    ///     Load a single plugin
    /// </summary>
    /// <param name="pluginInfo">The information of the plugin</param>
    /// <param name="pluginAssembly">The assembly containing the plugin</param>
    /// <returns>The plugin instance</returns>
    protected virtual TPlugin LoadPlugin(PluginInfo pluginInfo, Assembly pluginAssembly)
    {
        var type = pluginAssembly.GetType(pluginInfo.TypeName);
        var metadata = MetadataHelper.GetMetadata(type);
        if (metadata == null)
            throw new InvalidOperationException("Can't create an instance of " + GetType().FullName +
                                                " because it inherits from BaseUnityPlugin and the BepInPlugin attribute is missing.");

        var pluginInstance = (TPlugin) Activator.CreateInstance(type);
        pluginInstance.Info = Plugins[metadata.Guid];
        pluginInstance.Info.Instance = pluginInstance;
        pluginInstance.Logger = Logger.CreateLogSource(metadata.Name);
        pluginInstance.Config = new ConfigFile(Utility.CombinePaths(Paths.ConfigPath, metadata.Guid + ".cfg"), false, metadata);

        return pluginInstance;
    }
}
