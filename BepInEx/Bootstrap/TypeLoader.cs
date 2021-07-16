using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using BepInEx.Logging;
using Mono.Cecil;

namespace BepInEx.Bootstrap
{
	/// <summary>
	/// A cacheable metadata item. Can be used with <see cref="TypeLoader.LoadAssemblyCache{T}"/> and <see cref="TypeLoader.SaveAssemblyCache{T}"/> to cache plugin metadata.
	/// </summary>
	public interface ICacheable
	{
		/// <summary>
		/// Serialize the object into a binary format.
		/// </summary>
		/// <param name="bw"></param>
		void Save(BinaryWriter bw);

		/// <summary>
		/// Loads the object from binary format.
		/// </summary>
		/// <param name="br"></param>
		void Load(BinaryReader br);
	}

	/// <summary>
	/// A cached assembly.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class CachedAssembly<T> where T : ICacheable
	{
		/// <summary>
		/// List of cached items inside the assembly.
		/// </summary>
		public List<T> CacheItems { get; set; }


		/// <summary>
		/// Timestamp of the assembly. Used to check the age of the cache.
		/// </summary>
		public long Timestamp { get; set; }
	}

	/// <summary>
	///     Provides methods for loading specified types from an assembly.
	/// </summary>
	public static class TypeLoader
	{
		/// <summary>
		/// Default assembly resolved used by the <see cref="TypeLoader"/>
		/// </summary>
		public static readonly DefaultAssemblyResolver Resolver;
		
		/// <summary>
		/// Default reader parameters used by <see cref="TypeLoader"/>
		/// </summary>
		public static readonly ReaderParameters ReaderParameters;

		static TypeLoader()
		{
			Resolver = new DefaultAssemblyResolver();
			ReaderParameters = new ReaderParameters { AssemblyResolver = Resolver };

			Resolver.ResolveFailure += (sender, reference) =>
			{
				if (!Utility.TryParseAssemblyName(reference.FullName, out var name))
					return AssemblyResolve?.Invoke(sender, reference);

				var resolvePaths = new[]
				{
					Paths.BepInExAssemblyDirectory,
					Paths.PluginPath,
					Paths.PatcherPluginPath
				}.Concat(Paths.DllSearchPaths);
				
				foreach (string dllSearchPath in resolvePaths)
					if (Utility.TryResolveDllAssembly(name, dllSearchPath, ReaderParameters, out var assembly))
						return assembly;

				return AssemblyResolve?.Invoke(sender, reference);
			};
		}

		/// <summary>
		/// Event fired when <see cref="TypeLoader"/> fails to resolve a type during type loading.
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
		/// <returns>A dictionary of all assemblies in the directory and the list of type metadatas of types that match the selector.</returns>
        public static Dictionary<string, List<T>> FindPluginTypes<T>(string directory, Func<TypeDefinition, T> typeSelector, Func<AssemblyDefinition, bool> assemblyFilter = null, string cacheName = null) where T : ICacheable, new()
		{
			var result = new Dictionary<string, List<T>>();
			Dictionary<string, CachedAssembly<T>> cache = null;

			if (cacheName != null)
				cache = LoadAssemblyCache<T>(cacheName);

			foreach (string dll in Directory.GetFiles(Path.GetFullPath(directory), "*.dll", SearchOption.AllDirectories))
				try
				{
					if (cache != null && cache.TryGetValue(dll, out var cacheEntry))
					{
						long lastWrite = File.GetLastWriteTimeUtc(dll).Ticks;
						if (lastWrite == cacheEntry.Timestamp)
						{
							result[dll] = cacheEntry.CacheItems;
							continue;
						}
					}

					var ass = AssemblyDefinition.ReadAssembly(dll, ReaderParameters);

					if (!assemblyFilter?.Invoke(ass) ?? false)
					{
						result[dll] = new List<T>();
						ass.Dispose();
						continue;
					}

					var matches = ass.MainModule.Types.Select(typeSelector).Where(t => t != null).ToList();
					result[dll] = matches;
					ass.Dispose();
				}
				catch (BadImageFormatException e)
				{
					Logger.LogDebug($"Skipping loading {dll} because it's not a valid .NET assembly. Full error: {e.Message}");
				}
				catch (Exception e)
				{
					Logger.LogError(e.ToString());
				}

			if (cacheName != null)
				SaveAssemblyCache(cacheName, result);

			return result;
		}

		/// <summary>
		///     Loads an index of type metadatas from a cache.
		/// </summary>
		/// <param name="cacheName">Name of the cache</param>
		/// <typeparam name="T">Cacheable item</typeparam>
		/// <returns>Cached type metadatas indexed by the path of the assembly that defines the type. If no cache is defined, return null.</returns>
		public static Dictionary<string, CachedAssembly<T>> LoadAssemblyCache<T>(string cacheName) where T : ICacheable, new()
		{
			if (!EnableAssemblyCache.Value)
				return null;

			var result = new Dictionary<string, CachedAssembly<T>>();
			try
			{
				string path = Path.Combine(Paths.CachePath, $"{cacheName}_typeloader.dat");
				if (!File.Exists(path))
					return null;

				using (var br = new BinaryReader(File.OpenRead(path)))
				{
					int entriesCount = br.ReadInt32();

					for (var i = 0; i < entriesCount; i++)
					{
						string entryIdentifier = br.ReadString();
						long entryDate = br.ReadInt64();
						int itemsCount = br.ReadInt32();
						var items = new List<T>();

						for (var j = 0; j < itemsCount; j++)
						{
							var entry = new T();
							entry.Load(br);
							items.Add(entry);
						}

						result[entryIdentifier] = new CachedAssembly<T> { Timestamp = entryDate, CacheItems = items };
					}
				}
			}
			catch (Exception e)
			{
				Logger.LogWarning($"Failed to load cache \"{cacheName}\"; skipping loading cache. Reason: {e.Message}.");
			}

			return result;
		}

		/// <summary>
		///     Saves indexed type metadata into a cache.
		/// </summary>
		/// <param name="cacheName">Name of the cache</param>
		/// <param name="entries">List of plugin metadatas indexed by the path to the assembly that contains the types</param>
		/// <typeparam name="T">Cacheable item</typeparam>
		public static void SaveAssemblyCache<T>(string cacheName, Dictionary<string, List<T>> entries) where T : ICacheable
		{
			if (!EnableAssemblyCache.Value)
				return;

			try
			{
				if (!Directory.Exists(Paths.CachePath))
					Directory.CreateDirectory(Paths.CachePath);

				string path = Path.Combine(Paths.CachePath, $"{cacheName}_typeloader.dat");

				using (var bw = new BinaryWriter(File.OpenWrite(path)))
				{
					bw.Write(entries.Count);

					foreach (var kv in entries)
					{
						bw.Write(kv.Key);
						bw.Write(File.GetLastWriteTimeUtc(kv.Key).Ticks);
						bw.Write(kv.Value.Count);

						foreach (var item in kv.Value)
							item.Save(bw);
					}
				}
			}
			catch (Exception e)
			{
				Logger.LogWarning($"Failed to save cache \"{cacheName}\"; skipping saving cache. Reason: {e.Message}.");
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

		#region Config

		private static readonly ConfigEntry<bool> EnableAssemblyCache = ConfigFile.CoreConfig.Bind(
			"Caching", "EnableAssemblyCache", 
			true, 
			"Enable/disable assembly metadata cache\nEnabling this will speed up discovery of plugins and patchers by caching the metadata of all types BepInEx discovers.");

		#endregion
	}
}