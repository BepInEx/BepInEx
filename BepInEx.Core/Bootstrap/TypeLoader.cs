using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using BepInEx.Logging;
using Mono.Cecil;

namespace BepInEx.Bootstrap;

internal class CachedPluginLoadContext : IPluginLoadContext, IDisposable
{
    public IPluginLoadContext PluginLoadContext { get; }
    private byte[] assemblyData;
    private byte[] assemblySymbolsData;

    public CachedPluginLoadContext(IPluginLoadContext pluginLoadContext)
    {
        PluginLoadContext = pluginLoadContext;
    }

    public string AssemblyIdentifier => PluginLoadContext.AssemblyIdentifier;
    public string AssemblyHash => PluginLoadContext.AssemblyHash;
    public byte[] GetAssemblyData()
    {
        return assemblyData ??= PluginLoadContext.GetAssemblyData();
    }

    public byte[] GetAssemblySymbolsData()
    {
        return assemblySymbolsData ??= PluginLoadContext.GetAssemblySymbolsData();
    }

    public byte[] GetFile(string relativePath)
    {
        return PluginLoadContext.GetFile(relativePath);
    }

    public void Dispose()
    {
        assemblyData = null;
        assemblySymbolsData = null;
    }
}

/// <summary>
///     A cacheable metadata item. Can be used with <see cref="TypeLoader.LoadAssemblyCache{T}" /> and
///     <see cref="TypeLoader.SaveAssemblyCache{T}" /> to cache plugin metadata.
/// </summary>
public interface ICacheable
{
    /// <summary>
    ///     Serialize the object into a binary format.
    /// </summary>
    /// <param name="bw"></param>
    void Save(BinaryWriter bw);

    /// <summary>
    ///     Loads the object from binary format.
    /// </summary>
    /// <param name="br"></param>
    void Load(BinaryReader br);
}

/// <summary>
///     A cached assembly.
/// </summary>
/// <typeparam name="T"></typeparam>
public class CachedAssembly<T> where T : ICacheable
{
    /// <summary>
    ///     The version of the cache which increments on each format changes
    /// </summary>
    public const int Version = 0;

    /// <summary>
    ///     List of cached items inside the assembly.
    /// </summary>
    public List<T> CacheItems { get; set; }

    /// <summary>
    ///     Hash of the assembly. Used to verify that the assembly hasn't been changed.
    /// </summary>
    public string Hash { get; set; }
}

/// <summary>
///     Provides methods for loading specified types from an assembly.
/// </summary>
public static class TypeLoader
{
    /// <summary>
    ///     Default assembly resolved used by the <see cref="TypeLoader" />
    /// </summary>
    public static readonly DefaultAssemblyResolver CecilResolver;

    /// <summary>
    ///     Default reader parameters used by <see cref="TypeLoader" />
    /// </summary>
    public static readonly ReaderParameters ReaderParameters;

    public static HashSet<string> SearchDirectories = new();

    #region Config

    private static readonly ConfigEntry<bool> EnableAssemblyCache = ConfigFile.CoreConfig.Bind(
     "Caching", "EnableAssemblyCache",
     true,
     "Enable/disable assembly metadata cache\nEnabling this will speed up discovery of plugins and patchers by caching the metadata of all types BepInEx discovers.");

    #endregion

    static TypeLoader()
    {
        CecilResolver = new DefaultAssemblyResolver();
        ReaderParameters = new ReaderParameters { AssemblyResolver = CecilResolver };

        CecilResolver.ResolveFailure += CecilResolveOnFailure;
    }

    public static AssemblyDefinition CecilResolveOnFailure(object sender, AssemblyNameReference reference)
    {
        if (!Utility.TryParseAssemblyName(reference.FullName, out var name))
            return null;

        var resolveDirs = new[]
        {
            Paths.BepInExAssemblyDirectory,
            Paths.PluginPath,
            Paths.PluginProviderPath,
            Paths.PatcherPluginPath,
            Paths.PatcherProviderPath,
            Paths.ManagedPath
        }.Concat(SearchDirectories);

        foreach (var dir in resolveDirs)
        {
            if (!Directory.Exists(dir))
            {
                Logger.Log(LogLevel.Debug, $"Unable to resolve cecil search directory '{dir}'");
                continue;
            }

            if (Utility.TryResolveDllAssembly(name, dir, ReaderParameters, out var assembly))
                return assembly;
        }

        return AssemblyResolve?.Invoke(sender, reference);
    }

    /// <summary>
    ///     Event fired when <see cref="TypeLoader" /> fails to resolve a type during type loading.
    /// </summary>
    public static event AssemblyResolveEventHandler AssemblyResolve;

    /// <summary>
    ///     Looks up assemblies in the given directory and locates all types that can be loaded and collects their metadata.
    /// </summary>
    /// <typeparam name="T">The specific base type to search for.</typeparam>
    /// <param name="directory">The directory to search for assemblies.</param>
    /// <param name="typeSelector">A function to check if a type should be selected and to build the type metadata.</param>
    /// <param name="assemblyFilter">A filter function to quickly determine if the assembly can be loaded.</param>
    /// <param name="cacheName">The name of the cache to get cached types from.</param>
    /// <returns>
    ///     A dictionary of all assemblies in the directory and the list of type metadatas of types that match the
    ///     selector.
    /// </returns>
    public static Dictionary<string, List<T>> FindPluginTypes<T>(string directory,
                                                                 Func<TypeDefinition, IPluginLoadContext, string, T> typeSelector,
                                                                 Func<AssemblyDefinition, bool> assemblyFilter = null,
                                                                 string cacheName = null)
        where T : ICacheable, new()
    {
        var result = new Dictionary<string, List<T>>();
        var hashes = new Dictionary<string, string>();
        Dictionary<string, CachedAssembly<T>> cache = null;

        if (cacheName != null)
            cache = LoadAssemblyCache<T>(cacheName);

        foreach (var dll in Directory.GetFiles(Path.GetFullPath(directory), "*.dll", SearchOption.AllDirectories))
            try
            {
                using var dllMs = new MemoryStream(File.ReadAllBytes(dll));
                var hash = Utility.HashStream(dllMs);
                hashes[dll] = hash;
                dllMs.Position = 0;
                if (cache != null && cache.TryGetValue(dll, out var cacheEntry))
                    if (hash == cacheEntry.Hash)
                    {
                        result[dll] = cacheEntry.CacheItems;
                        continue;
                    }

                result[dll] = ExamineStream(typeSelector, assemblyFilter, dllMs, null, dll);
            }
            catch (BadImageFormatException e)
            {
                Logger.Log(LogLevel.Debug,
                           $"Skipping loading {dll} because it's not a valid .NET assembly. Full error: {e.Message}");
            }
            catch (Exception e)
            {
                Logger.Log(LogLevel.Error, e);
            }

        if (cacheName != null)
            SaveAssemblyCache(cacheName, result, hashes);

        return result;
    }

    /// <summary>
    ///     Looks up assemblies using the given loaders and locates all types that can be loaded and collects their metadata.
    /// </summary>
    /// <typeparam name="T">The specific base type to search for.</typeparam>
    /// <param name="loadContexts">The load contexts to obtain the assemblies from.</param>
    /// <param name="typeSelector">A function to check if a type should be selected and to build the type metadata.</param>
    /// <param name="assemblyFilter">A filter function to quickly determine if the assembly can be loaded.</param>
    /// <param name="cacheName">The name of the cache to get cached types from.</param>
    /// <returns>
    ///     A dictionary of all assemblies in the directory and the list of type metadatas of types that match the
    ///     selector.
    /// </returns>
    public static List<T> GetPluginsFromLoadContexts<T>(IEnumerable<IPluginLoadContext> loadContexts,
                                                        Func<TypeDefinition, IPluginLoadContext, string, T> typeSelector,
                                                        Func<AssemblyDefinition, bool> assemblyFilter = null,
                                                        string cacheName = null)
        where T : ICacheable, new()
    {
        var result = new Dictionary<string, List<T>>();
        var hashes = new Dictionary<string, string>();
        Dictionary<string, CachedAssembly<T>> cache = null;

        if (cacheName != null)
            cache = LoadAssemblyCache<T>(cacheName);

        foreach (IPluginLoadContext loadContext in loadContexts)
        {
            IList<T> plugins;
            if (cache != null && loadContext.AssemblyHash != null &&
                cache.TryGetValue(loadContext.AssemblyIdentifier, out var cacheEntry) &&
                loadContext.AssemblyHash == cacheEntry.Hash)
            {
                plugins = cacheEntry.CacheItems;
            }
            else
            {
                var assemblyData = loadContext.GetAssemblyData();
                using var memory = new MemoryStream(assemblyData);
                plugins = ExamineStream(typeSelector, assemblyFilter, memory, loadContext, null);
            }
                
            foreach (T pluginInfo in plugins)
            {
                if (!result.ContainsKey(loadContext.AssemblyIdentifier))
                    result[loadContext.AssemblyIdentifier] = new();
                result[loadContext.AssemblyIdentifier].Add(pluginInfo);
            }
        }
            
        if (cache != null)
            SaveAssemblyCache(cacheName, result, hashes);

        return result.SelectMany(x => x.Value).ToList();
    }

    private static List<T> ExamineStream<T>(Func<TypeDefinition, IPluginLoadContext, string, T> typeSelector,
                                           Func<AssemblyDefinition, bool> assemblyFilter,
                                           MemoryStream dllMs,
                                           IPluginLoadContext loadContext,
                                           string location)
        where T : ICacheable, new()
    {
        using var ass = AssemblyDefinition.ReadAssembly(dllMs, ReaderParameters);
        Logger.Log(LogLevel.Debug, $"Examining '{ass.Name}'");

        if (!assemblyFilter?.Invoke(ass) ?? false)
        {
            return new List<T>();
        }

        var matches = ass.MainModule.Types
                         .Select(t => typeSelector(t, loadContext, location))
                         .Where(t => t != null).ToList();
        return matches;
    }

    /// <summary>
    ///     Loads an index of type metadatas from a cache.
    /// </summary>
    /// <param name="cacheName">Name of the cache</param>
    /// <typeparam name="T">Cacheable item</typeparam>
    /// <returns>
    ///     Cached type metadatas indexed by the path of the assembly that defines the type. If no cache is defined,
    ///     return null.
    /// </returns>
    public static Dictionary<string, CachedAssembly<T>> LoadAssemblyCache<T>(string cacheName)
        where T : ICacheable, new()
    {
        if (!EnableAssemblyCache.Value)
            return null;

        var result = new Dictionary<string, CachedAssembly<T>>();
        try
        {
            var path = Path.Combine(Paths.CachePath, $"{cacheName}_typeloader.dat");
            if (!File.Exists(path))
                return null;

            using var br = new BinaryReader(File.OpenRead(path));
            var version = br.ReadInt32();
            if (version == CachedAssembly<T>.Version)
            {
                var entriesCount = br.ReadInt32();

                for (var i = 0; i < entriesCount; i++)
                {
                    var entryIdentifier = br.ReadString();
                    var hash = br.ReadString();
                    var itemsCount = br.ReadInt32();
                    var items = new List<T>();

                    for (var j = 0; j < itemsCount; j++)
                    {
                        var entry = new T();
                        entry.Load(br);
                        items.Add(entry);
                    }

                    result[entryIdentifier] = new CachedAssembly<T> { Hash = hash, CacheItems = items };
                }
            }
        }
        catch (Exception e)
        {
            Logger.Log(LogLevel.Warning,
                       $"Failed to load cache \"{cacheName}\"; skipping loading cache. Reason: {e.Message}.");
        }

        return result;
    }

    /// <summary>
    ///     Saves indexed type metadata into a cache.
    /// </summary>
    /// <param name="cacheName">Name of the cache</param>
    /// <param name="entries">List of plugin metadatas indexed by the path to the assembly that contains the types</param>
    /// <param name="hashes">Hash values that can be used for checking similarity between cached and live assembly</param>
    /// <typeparam name="T">Cacheable item</typeparam>
    public static void SaveAssemblyCache<T>(string cacheName,
                                            Dictionary<string, List<T>> entries,
                                            Dictionary<string, string> hashes)
        where T : ICacheable
    {
        if (!EnableAssemblyCache.Value)
            return;

        try
        {
            if (!Directory.Exists(Paths.CachePath))
                Directory.CreateDirectory(Paths.CachePath);

            var path = Path.Combine(Paths.CachePath, $"{cacheName}_typeloader.dat");

            using var bw = new BinaryWriter(File.OpenWrite(path));
            bw.Write(CachedAssembly<T>.Version);
            bw.Write(entries.Count);

            foreach (var kv in entries)
            {
                bw.Write(kv.Key);
                bw.Write(hashes.TryGetValue(kv.Key, out var hash) ? hash : "");
                bw.Write(kv.Value.Count);

                foreach (var item in kv.Value)
                    item.Save(bw);
            }
        }
        catch (Exception e)
        {
            Logger.Log(LogLevel.Warning,
                       $"Failed to save cache \"{cacheName}\"; skipping saving cache. Reason: {e.Message}.");
        }
    }

    /// <summary>
    ///     Converts TypeLoadException to a readable string.
    /// </summary>
    /// <param name="ex">TypeLoadException</param>
    /// <returns>Readable representation of the exception</returns>
    public static string TypeLoadExceptionToString(ReflectionTypeLoadException ex)
    {
        var sb = new StringBuilder();
        foreach (var exSub in ex.LoaderExceptions)
        {
            sb.AppendLine(exSub.Message);
            if (exSub is FileNotFoundException exFileNotFound)
            {
                if (!string.IsNullOrEmpty(exFileNotFound.FusionLog))
                {
                    sb.AppendLine("Fusion Log:");
                    sb.AppendLine(exFileNotFound.FusionLog);
                }
            }
            else if (exSub is FileLoadException exLoad)
            {
                if (!string.IsNullOrEmpty(exLoad.FusionLog))
                {
                    sb.AppendLine("Fusion Log:");
                    sb.AppendLine(exLoad.FusionLog);
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
