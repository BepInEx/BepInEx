﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Bootstrap;
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
	public delegate void AssemblyPatcherDelegate(ref AssemblyDefinition assembly);

	/// <summary>
	///     Worker class which is used for loading and patching entire folders of assemblies, or alternatively patching and
	///     loading assemblies one at a time.
	/// </summary>
	public static class AssemblyPatcher
	{
		private const BindingFlags ALL = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.IgnoreCase;

		/// <summary>
		/// List of all patcher plugins to be applied
		/// </summary>
		public static List<PatcherPlugin> PatcherPlugins { get; } = new List<PatcherPlugin>();

		// We make use the safe version for possible cases where a mod loader wants to modify the original patcher
		// plugins list
		private static IEnumerable<PatcherPlugin> PatcherPluginsSafe => PatcherPlugins.ToList();

		private static readonly string DumpedAssembliesPath = Utility.CombinePaths(Paths.BepInExRootPath, "DumpedAssemblies", Paths.ProcessName);

		private static readonly  Dictionary<string, string> DumpedAssemblyPaths = new Dictionary<string, string>();

		/// <summary>
		///     Adds a single assembly patcher to the pool of applicable patches.
		/// </summary>
		/// <param name="patcher">Patcher to apply.</param>
		public static void AddPatcher(PatcherPlugin patcher)
		{
			PatcherPlugins.Add(patcher);
		}

		private static T CreateDelegate<T>(MethodInfo method) where T : class => method != null ? Delegate.CreateDelegate(typeof(T), method) as T : null;

		private static PatcherPlugin ToPatcherPlugin(TypeDefinition type)
		{
			if (type.IsInterface || type.IsAbstract && !type.IsSealed)
				return null;

			var targetDlls = type.Methods.FirstOrDefault(m => m.Name.Equals("get_TargetDLLs", StringComparison.InvariantCultureIgnoreCase) &&
															  m.IsPublic &&
															  m.IsStatic);

			if (targetDlls == null ||
				targetDlls.ReturnType.FullName != "System.Collections.Generic.IEnumerable`1<System.String>")
				return null;

			var patch = type.Methods.FirstOrDefault(m => m.Name.Equals("Patch") &&
														 m.IsPublic &&
														 m.IsStatic &&
														 m.ReturnType.FullName == "System.Void" &&
														 m.Parameters.Count == 1 &&
														 (m.Parameters[0].ParameterType.FullName == "Mono.Cecil.AssemblyDefinition&" ||
														  m.Parameters[0].ParameterType.FullName == "Mono.Cecil.AssemblyDefinition"));

			if (patch == null)
				return null;

			return new PatcherPlugin
			{
				TypeName = type.FullName
			};
		}

		/// <summary>
		///     Adds all patchers from all managed assemblies specified in a directory.
		/// </summary>
		/// <param name="directory">Directory to search patcher DLLs from.</param>
		public static void AddPatchersFromDirectory(string directory)
        {
            if (!Directory.Exists(directory))
                return;

            CleanUpOldBepGui(directory);

            var sortedPatchers = new SortedDictionary<string, PatcherPlugin>();

            var patchers = TypeLoader.FindPluginTypes(directory, ToPatcherPlugin);

            foreach (var keyValuePair in patchers)
            {
                var assemblyPath = keyValuePair.Key;
                var patcherCollection = keyValuePair.Value;

                if (patcherCollection.Count == 0)
                    continue;

                var ass = Assembly.LoadFile(assemblyPath);

                foreach (var patcherPlugin in patcherCollection)
                {
                    try
                    {
                        var type = ass.GetType(patcherPlugin.TypeName);

                        var methods = type.GetMethods(ALL);

                        patcherPlugin.Initializer = CreateDelegate<Action>(methods.FirstOrDefault(m => m.Name.Equals("Initialize", StringComparison.InvariantCultureIgnoreCase) &&
                                                                                                       m.GetParameters().Length == 0 &&
                                                                                                       m.ReturnType == typeof(void)));

                        patcherPlugin.Finalizer = CreateDelegate<Action>(methods.FirstOrDefault(m => m.Name.Equals("Finish", StringComparison.InvariantCultureIgnoreCase) &&
                                                                                                     m.GetParameters().Length == 0 &&
                                                                                                     m.ReturnType == typeof(void)));

                        patcherPlugin.TargetDLLs = CreateDelegate<Func<IEnumerable<string>>>(type.GetProperty("TargetDLLs", ALL).GetGetMethod());

                        var patcher = methods.FirstOrDefault(m => m.Name.Equals("Patch", StringComparison.CurrentCultureIgnoreCase) &&
                                                                  m.ReturnType == typeof(void) &&
                                                                  m.GetParameters().Length == 1 &&
                                                                  (m.GetParameters()[0].ParameterType == typeof(AssemblyDefinition) ||
                                                                   m.GetParameters()[0].ParameterType == typeof(AssemblyDefinition).MakeByRefType()));

                        patcherPlugin.Patcher = (ref AssemblyDefinition pAss) =>
                        {
                            //we do the array fuckery here to get the ref result out
                            object[] args = { pAss };

                            patcher.Invoke(null, args);

                            pAss = (AssemblyDefinition)args[0];
                        };

                        sortedPatchers.Add($"{ass.GetName().Name}/{type.FullName}", patcherPlugin);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError($"Failed to load patcher [{patcherPlugin.TypeName}]: {e.Message}");
                        if (e is ReflectionTypeLoadException re)
                            Logger.LogDebug(TypeLoader.TypeLoadExceptionToString(re));
                        else
                            Logger.LogDebug(e.ToString());
                    }
                }

                var assName = ass.GetName();
                Logger.Log(patcherCollection.Any() ? LogLevel.Info : LogLevel.Debug,
                    $"Loaded {patcherCollection.Count} patcher method{(patcherCollection.Count == 1 ? "" : "s")} from [{assName.Name} {assName.Version}]");
            }

            foreach (KeyValuePair<string, PatcherPlugin> patcher in sortedPatchers)
                AddPatcher(patcher.Value);
        }

        private static void CleanUpOldBepGui(string directory)
        {
            var deletedOldBepGui = false;
            try
            {
                // Remove old BepInEx GUI otherwise both will get loaded
                foreach (var directoryPath in Directory.GetDirectories(directory, $"BepInEx_GUI_*_v0*", SearchOption.AllDirectories))
                {
                    Logger.LogInfo($"Deleting folder: {directoryPath}");
                    Directory.Delete(directoryPath, true);
                    deletedOldBepGui = true;
                }

				if (deletedOldBepGui)
				{
					Logger.LogInfo($"Removing old Bep Gui config");
					foreach (var file in Directory.GetFiles(Paths.ConfigPath, $"BepInEx.GUI.cfg", SearchOption.AllDirectories))
					{
						File.Delete(file);
					}
				}
			}
            catch (Exception e)
            {
				Logger.LogDebug(e);
			}
        }

        private static void InitializePatchers()
		{
			foreach (var assemblyPatcher in PatcherPluginsSafe)
			{
				try
				{
					assemblyPatcher.Initializer?.Invoke();
				}
				catch (Exception e)
				{
					Logger.LogError($"Failed to run Initializer of {assemblyPatcher.TypeName}: {e}");
				}
			}
		}

		private static void FinalizePatching()
		{
			foreach (var assemblyPatcher in PatcherPluginsSafe)
			{
				try
				{
					assemblyPatcher.Finalizer?.Invoke();
				}
				catch (Exception e)
				{
					Logger.LogError($"Failed to run Finalizer of {assemblyPatcher.TypeName}: {e}");
				}
			}
		}

		/// <summary>
		///     Releases all patchers to let them be collected by GC.
		/// </summary>
		public static void DisposePatchers()
		{
			PatcherPlugins.Clear();
		}

		private static string GetAssemblyName(string fullName)
		{
			return Utility.TryParseAssemblyName(fullName, out var assName) ? assName.Name : fullName;
		}

		/// <summary>
		///     Applies patchers to all assemblies in the given directory and loads patched assemblies into memory.
		/// </summary>
		/// <param name="directories">Directories to load CLR assemblies from in their search order.</param>
		public static void PatchAndLoad(params string[] directories)
		{
			// First, load patchable assemblies into Cecil
			// Ignore case for keys (dll filenames) to account for running on *nix
			var assemblies = new Dictionary<string, AssemblyDefinition>(StringComparer.InvariantCultureIgnoreCase);

			foreach (string assemblyPath in Utility.GetUniqueFilesInDirectories(directories, "*.dll"))
			{
				AssemblyDefinition assembly;

				try
				{
					assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
				}
				catch (BadImageFormatException)
				{
					// Not a managed assembly, skip
					continue;
				}

				//NOTE: this is special case here because the dependency handling for System.dll is a bit wonky
				//System has an assembly reference to itself, and it also has a reference to Mono.Security causing a circular dependency
				//It's also generally dangerous to change system.dll since so many things rely on it,
				// and it's already loaded into the appdomain since this loader references it, so we might as well skip it
				if (assembly.Name.Name == "System" || assembly.Name.Name == "mscorlib") //mscorlib is already loaded into the appdomain so it can't be patched
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
			var patchedAssemblies = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
			var resolvedAssemblies = new Dictionary<string, string>();
			// TODO: Maybe instead reload the assembly and repatch with other valid patchers?
			var invalidAssemblies = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
			foreach (var assemblyPatcher in PatcherPluginsSafe)
				foreach (string targetDll in assemblyPatcher.TargetDLLs())
					if (assemblies.TryGetValue(targetDll, out var assembly) && !invalidAssemblies.Contains(targetDll))
					{
						Logger.LogInfo($"Patching [{assembly.Name.Name}] with [{assemblyPatcher.TypeName}]");

						try
						{
							assemblyPatcher.Patcher?.Invoke(ref assembly);
						}
						catch (Exception e)
						{
							Logger.LogError($"Failed to run [{assemblyPatcher.TypeName}] when patching [{assembly.Name.Name}]. This assembly will not be patched. Error: {e}");
							patchedAssemblies.Remove(targetDll);
							invalidAssemblies.Add(targetDll);
							continue;
						}

						assemblies[targetDll] = assembly;
						patchedAssemblies.Add(targetDll);

						foreach (var resolvedAss in AppDomain.CurrentDomain.GetAssemblies())
						{
							var name = GetAssemblyName(resolvedAss.FullName);
							// Report only the first type that caused the assembly to load, because any subsequent ones can be false positives
							if (!resolvedAssemblies.ContainsKey(name))
								resolvedAssemblies[name] = assemblyPatcher.TypeName;
						}
					}

			// Check if any patched assemblies have been already resolved by the CLR
			// If there are any, they cannot be loaded by the preloader
			var patchedAssemblyNames = new HashSet<string>(assemblies.Where(kv => patchedAssemblies.Contains(kv.Key)).Select(kv => kv.Value.Name.Name), StringComparer.InvariantCultureIgnoreCase);
			var earlyLoadAssemblies = resolvedAssemblies.Where(kv => patchedAssemblyNames.Contains(kv.Key)).ToList();

			if (earlyLoadAssemblies.Count != 0)
			{
				Logger.LogWarning(new StringBuilder()
								 .AppendLine("The following assemblies have been loaded too early and will not be patched by preloader:")
								 .AppendLine(string.Join(Environment.NewLine, earlyLoadAssemblies.Select(kv => $"* [{kv.Key}] (first loaded by [{kv.Value}])").ToArray()))
								 .AppendLine("Expect unexpected behavior and issues with plugins and patchers not being loaded.")
								 .ToString());
			}

			DumpedAssemblyPaths.Clear();
			// Finally, load patched assemblies into memory
			if (ConfigDumpAssemblies.Value || ConfigLoadDumpedAssemblies.Value)
			{
				if (!Directory.Exists(DumpedAssembliesPath))
					Directory.CreateDirectory(DumpedAssembliesPath);

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
						var path = Path.Combine(DumpedAssembliesPath, $"{name}{postfix}{ext}");
						if (!Utility.TryOpenFileStream(path, FileMode.Create, out var fs))
							continue;
						assembly.Write(fs);
						fs.Dispose();
						DumpedAssemblyPaths[filename] = path;
						break;
					}
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
		/// <param name="filename">File name of the assembly being loaded.</param>
		public static void Load(AssemblyDefinition assembly, string filename)
		{
			if (ConfigLoadDumpedAssemblies.Value && DumpedAssemblyPaths.TryGetValue(filename, out var dumpedAssemblyPath))
				Assembly.LoadFile(dumpedAssemblyPath);
			else
				using (var assemblyStream = new MemoryStream())
				{
					assembly.Write(assemblyStream);
					Assembly.Load(assemblyStream.ToArray());
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
}
