using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using BepInEx.Preloader.RuntimeFixes;
using Mono.Cecil;

namespace BepInEx.Preloader.Patching
{
	/// <summary>
	///     Delegate used in patching assemblies.
	/// </summary>
	/// <param name="assembly">The assembly that is being patched.</param>
	internal delegate void AssemblyPatcherDelegate(ref AssemblyDefinition assembly);

	/// <summary>
	///     Worker class which is used for loading and patching entire folders of assemblies, or alternatively patching and
	///     loading assemblies one at a time.
	/// </summary>
	internal static class AssemblyPatcher
	{
		private static readonly List<PatcherPlugin> patchers = new List<PatcherPlugin>();

		/// <summary>
		///     Configuration value of whether assembly dumping is enabled or not.
		/// </summary>
		private static bool DumpingEnabled =>
			Utility.SafeParseBool(Config.GetEntry("dump-assemblies", "false", "Preloader"));

		/// <summary>
		///     Adds a single assembly patcher to the pool of applicable patches.
		/// </summary>
		/// <param name="patcher">Patcher to apply.</param>
		public static void AddPatcher(PatcherPlugin patcher)
		{
			patchers.Add(patcher);
		}

		/// <summary>
		///     Adds all patchers from all managed assemblies specified in a directory.
		/// </summary>
		/// <param name="directory">Directory to search patcher DLLs from.</param>
		/// <param name="patcherLocator">A function that locates assembly patchers in a given managed assembly.</param>
		public static void AddPatchersFromDirectory(string directory,
			Func<Assembly, List<PatcherPlugin>> patcherLocator)
		{
			if (!Directory.Exists(directory))
				return;

			var sortedPatchers = new SortedDictionary<string, PatcherPlugin>();

			foreach (string assemblyPath in Directory.GetFiles(directory, "*.dll"))
				try
				{
					var assembly = Assembly.LoadFrom(assemblyPath);

					foreach (var patcher in patcherLocator(assembly))
						sortedPatchers.Add(patcher.Name, patcher);
				}
				catch (BadImageFormatException) { } //unmanaged DLL
				catch (ReflectionTypeLoadException) { } //invalid references

			foreach (KeyValuePair<string, PatcherPlugin> patcher in sortedPatchers)
				AddPatcher(patcher.Value);
		}

		private static void InitializePatchers()
		{
			foreach (var assemblyPatcher in patchers)
				assemblyPatcher.Initializer?.Invoke();
		}

		private static void FinalizePatching()
		{
			foreach (var assemblyPatcher in patchers)
				assemblyPatcher.Finalizer?.Invoke();
		}

		/// <summary>
		///     Releases all patchers to let them be collected by GC.
		/// </summary>
		public static void DisposePatchers()
		{
			patchers.Clear();
		}

		/// <summary>
		///     Applies patchers to all assemblies in the given directory and loads patched assemblies into memory.
		/// </summary>
		/// <param name="directory">Directory to load CLR assemblies from.</param>
		public static void PatchAndLoad(string directory)
		{
			// First, load patchable assemblies into Cecil
			var assemblies = new Dictionary<string, AssemblyDefinition>();

			foreach (string assemblyPath in Directory.GetFiles(directory, "*.dll"))
			{
				var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);

				//NOTE: this is special cased here because the dependency handling for System.dll is a bit wonky
				//System has an assembly reference to itself, and it also has a reference to Mono.Security causing a circular dependency
				//It's also generally dangerous to change system.dll since so many things rely on it, 
				// and it's already loaded into the appdomain since this loader references it, so we might as well skip it
				if (assembly.Name.Name == "System"
					|| assembly.Name.Name == "mscorlib"
				) //mscorlib is already loaded into the appdomain so it can't be patched
				{
					assembly.Dispose();
					continue;
				}

				if (UnityPatches.AssemblyLocations.ContainsKey(assembly.FullName))
				{
					Logger.Log(LogLevel.Warning,
						$"Tried to load duplicate assembly {Path.GetFileName(assemblyPath)} from Managed folder! Skipping...");
					continue;
				}

				assemblies.Add(Path.GetFileName(assemblyPath), assembly);
				UnityPatches.AssemblyLocations.Add(assembly.FullName, Path.GetFullPath(assemblyPath));
			}

			// Next, initialize all the patchers
			InitializePatchers();

			// Then, perform the actual patching
			var patchedAssemblies = new HashSet<string>();
			foreach (var assemblyPatcher in patchers)
				foreach (string targetDll in assemblyPatcher.TargetDLLs)
					if (assemblies.TryGetValue(targetDll, out var assembly))
					{
						assemblyPatcher.Patcher?.Invoke(ref assembly);
						assemblies[targetDll] = assembly;
						patchedAssemblies.Add(targetDll);
					}

			// Finally, load all assemblies into memory
			foreach (KeyValuePair<string, AssemblyDefinition> kv in assemblies)
			{
				string filename = kv.Key;
				var assembly = kv.Value;

				if (DumpingEnabled && patchedAssemblies.Contains(filename))
					using (var mem = new MemoryStream())
					{
						string dirPath = Path.Combine(Paths.BepInExRootPath, "DumpedAssemblies");

						if (!Directory.Exists(dirPath))
							Directory.CreateDirectory(dirPath);

						assembly.Write(mem);
						File.WriteAllBytes(Path.Combine(dirPath, filename), mem.ToArray());
					}

				Load(assembly);
				assembly.Dispose();
			}

			//run all finalizers
			FinalizePatching();
		}

		/// <summary>
		///     Loads an individual assembly defintion into the CLR.
		/// </summary>
		/// <param name="assembly">The assembly to load.</param>
		public static void Load(AssemblyDefinition assembly)
		{
			using (var assemblyStream = new MemoryStream())
			{
				assembly.Write(assemblyStream);
				Assembly.Load(assemblyStream.ToArray());
			}
		}
	}
}