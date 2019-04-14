using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
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
		public static List<PatcherPlugin> PatcherPlugins { get; } = new List<PatcherPlugin>();

		private static readonly string DumpedAssembliesPath = Path.Combine(Paths.BepInExRootPath, "DumpedAssemblies");

        /// <summary>
        ///     Adds a single assembly patcher to the pool of applicable patches.
        /// </summary>
        /// <param name="patcher">Patcher to apply.</param>
        public static void AddPatcher(PatcherPlugin patcher)
		{
			PatcherPlugins.Add(patcher);
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

			foreach (string assemblyPath in Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories))
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
			foreach (var assemblyPatcher in PatcherPlugins)
				assemblyPatcher.Initializer?.Invoke();
		}

		private static void FinalizePatching()
		{
			foreach (var assemblyPatcher in PatcherPlugins)
				assemblyPatcher.Finalizer?.Invoke();
		}

		/// <summary>
		///     Releases all patchers to let them be collected by GC.
		/// </summary>
		public static void DisposePatchers()
		{
			PatcherPlugins.Clear();
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
					Logger.LogWarning($"Tried to load duplicate assembly {Path.GetFileName(assemblyPath)} from Managed folder! Skipping...");
					continue;
				}

				assemblies.Add(Path.GetFileName(assemblyPath), assembly);
				UnityPatches.AssemblyLocations.Add(assembly.FullName, Path.GetFullPath(assemblyPath));
			}

			// Next, initialize all the patchers
			InitializePatchers();

			// Then, perform the actual patching
			var patchedAssemblies = new HashSet<string>();
			foreach (var assemblyPatcher in PatcherPlugins)
				foreach (string targetDll in assemblyPatcher.TargetDLLs)
					if (assemblies.TryGetValue(targetDll, out var assembly))
					{
						Logger.LogInfo($"Patching [{assembly.Name.Name}] with [{assemblyPatcher.Name}]");

						assemblyPatcher.Patcher?.Invoke(ref assembly);
						assemblies[targetDll] = assembly;
						patchedAssemblies.Add(targetDll);
					}


            // Finally, load patched assemblies into memory

			if (ConfigDumpAssemblies.Value || ConfigLoadDumpedAssemblies.Value)
			{
				if (!Directory.Exists(DumpedAssembliesPath))
					Directory.CreateDirectory(DumpedAssembliesPath);

				foreach (KeyValuePair<string, AssemblyDefinition> kv in assemblies)
				{
					string filename = kv.Key;
					var assembly = kv.Value;

					if (patchedAssemblies.Contains(filename))
						assembly.Write(Path.Combine(DumpedAssembliesPath, filename));
				}
            }

			if (ConfigBreakBeforeLoadAssemblies.Value)
			{
				Logger.LogInfo($"BepInEx is about load the following assemblies:\n{string.Join("\n", patchedAssemblies.ToArray())}");
				Logger.LogInfo($"The assemblies were dumped into {DumpedAssembliesPath}");
				Logger.LogInfo("Load any assemblies into the debugger, set breakpoints and continue execution.");
				Debugger.Break();
			}

			foreach (var kv in assemblies)
			{
				string filename = kv.Key;
				var assembly = kv.Value;

                // Note that since we only *load* assemblies, they shouldn't trigger dependency loading
                // Not loading all assemblies is very important not only because of memory reasons,
                // but because some games *rely* on that because of messed up internal dependencies.
                if (patchedAssemblies.Contains(filename))
					Load(assembly, filename);

				// Though we have to dispose of all assemblies regardless of them being patched or not
				assembly.Dispose();
            }

            //run all finalizers
            FinalizePatching();
		}

		/// <summary>
		///     Loads an individual assembly definition into the CLR.
		/// </summary>
		/// <param name="assembly">The assembly to load.</param>
		public static void Load(AssemblyDefinition assembly, string filename)
		{
			if (ConfigLoadDumpedAssemblies.Value)
				Assembly.LoadFile(Path.Combine(DumpedAssembliesPath, filename));
			else
				using (var assemblyStream = new MemoryStream())
				{
					assembly.Write(assemblyStream);
					Assembly.Load(assemblyStream.ToArray());
				}
		}

		#region Config

		private static readonly ConfigWrapper<bool> ConfigDumpAssemblies = ConfigFile.CoreConfig.Wrap(
			"Preloader",
			"DumpAssemblies",
			"If enabled, BepInEx will save patched assemblies into BepInEx/DumpedAssemblies.\nThis can be used by developers to inspect and debug preloader patchers.",
			false);

		private static readonly ConfigWrapper<bool> ConfigLoadDumpedAssemblies = ConfigFile.CoreConfig.Wrap(
			"Preloader",
			"LoadDumpedAssemblies",
            "If enabled, BepInEx will load patched assemblies from BepInEx/DumpedAssemblies instead of memory.\nThis can be used to be able to load patched assemblies into debuggers like dnSpy.\nIf set to true, will override DumpAssemblies.",
			false);

		private static readonly ConfigWrapper<bool> ConfigBreakBeforeLoadAssemblies = ConfigFile.CoreConfig.Wrap(
			"Preloader",
			"BreakBeforeLoadAssemblies",
			"If enabled, BepInEx will call Debugger.Break() once before loading patched assemblies.\nThis can be used with debuggers like dnSpy to install breakpoints into patched assemblies before they are loaded.",
			false);

        #endregion
    }
}