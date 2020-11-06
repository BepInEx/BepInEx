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
using Mono.Cecil;

namespace BepInEx.Preloader.Core
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
	public class AssemblyPatcher : IDisposable
	{
		private const BindingFlags ALL = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.IgnoreCase;

		/// <summary>
		/// A list of plugins that will be initialized and executed, in the order of the list.
		/// </summary>
		public List<PatcherPlugin> PatcherPlugins { get; } = new List<PatcherPlugin>();


		/// <summary>
		/// A cloned version of <see cref="PatcherPlugins"/> to ensure that any foreach loops do not break when the collection gets modified.
		/// </summary>
		private IEnumerable<PatcherPlugin> PatcherPluginsSafe => PatcherPlugins.ToList();

		/// <summary>
		/// <para>Contains a list of assemblies that will be patched and loaded into the runtime.</para>
		/// <para>The dictionary has the name of the file, without any directories. These are used by the dumping functionality, and as such, these are also required to be unique. They do not have to be exactly the same as the real filename, however they have to be mapped deterministically.</para>
		/// <para>Order is not respected, as it will be sorted by dependencies.</para>
		/// </summary>
		public Dictionary<string, AssemblyDefinition> AssembliesToPatch { get; } = new Dictionary<string, AssemblyDefinition>();

		/// <summary>
		/// <para>Contains a dictionary of assemblies that have been loaded as part of executing this assembly patcher..</para>
		/// <para>The key is the same key as used in <see cref="LoadedAssemblies"/>, while the value is the actual assembly itself.</para>
		/// </summary>
		public Dictionary<string, Assembly> LoadedAssemblies { get; } = new Dictionary<string, Assembly>();

		/// <summary>
		/// The directory location as to where patched assemblies will be saved to and loaded from disk, for debugging purposes. Defaults to BepInEx/DumpedAssemblies
		/// </summary>
		public string DumpedAssembliesPath { get; set; } = Path.Combine(Paths.BepInExRootPath, "DumpedAssemblies");

		public ManualLogSource Logger { get; } = BepInEx.Logging.Logger.CreateLogSource("AssemblyPatcher");

		private static T CreateDelegate<T>(MethodInfo method) where T : class => method != null ? Delegate.CreateDelegate(typeof(T), method) as T : null;

		private static PatcherPlugin ToPatcherPlugin(TypeDefinition type, string assemblyPath)
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
		public void AddPatchersFromDirectory(string directory)
		{
			if (!Directory.Exists(directory))
				return;

			var sortedPatchers = new SortedDictionary<string, PatcherPlugin>();

			var patchers = TypeLoader.FindPluginTypes(directory, ToPatcherPlugin);

			foreach (var keyValuePair in patchers)
			{
				var assemblyPath = keyValuePair.Key;
				var patcherCollection = keyValuePair.Value;

				if(patcherCollection.Count == 0)
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

				Logger.Log(patcherCollection.Any() ? LogLevel.Info : LogLevel.Debug,
					$"Loaded {patcherCollection.Count} patcher methods from {ass.GetName().FullName}");
			}

			foreach (KeyValuePair<string, PatcherPlugin> patcher in sortedPatchers)
				PatcherPlugins.Add(patcher.Value);
		}


		/// <summary>
		/// Adds all .dll assemblies in a directory to be patched and loaded by this patcher instance. Non-managed assemblies are skipped.
		/// </summary>
		/// <param name="directory">The directory to search.</param>
		public void LoadAssemblyDirectory(string directory)
		{
			LoadAssemblyDirectory(directory, "dll");
		}

		/// <summary>
		/// Adds all assemblies in a directory to be patched and loaded by this patcher instance. Non-managed assemblies are skipped.
		/// </summary>
		/// <param name="directory">The directory to search.</param>
		/// <param name="assemblyExtensions">The file extensions to attempt to load.</param>
		public void LoadAssemblyDirectory(string directory, params string[] assemblyExtensions)
		{
			var filesToSearch = assemblyExtensions
				.SelectMany(ext => Directory.GetFiles(directory, "*." + ext, SearchOption.TopDirectoryOnly));

			foreach (string assemblyPath in filesToSearch)
			{
				if (!TryLoadAssembly(assemblyPath, out var assembly))
					continue;

				// NOTE: this is special cased here because the dependency handling for System.dll is a bit wonky
				// System has an assembly reference to itself, and it also has a reference to Mono.Security causing a circular dependency
				// It's also generally dangerous to change system.dll since so many things rely on it, 
				// and it's already loaded into the appdomain since this loader references it, so we might as well skip it
				if (assembly.Name.Name == "System" || assembly.Name.Name == "mscorlib") //mscorlib is already loaded into the appdomain so it can't be patched
				{
					assembly.Dispose();
					continue;
				}

				AssembliesToPatch.Add(Path.GetFileName(assemblyPath), assembly);

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
		/// Attempts to load a managed assembly as an <see cref="AssemblyDefinition"/>. Returns true if successful.
		/// </summary>
		/// <param name="path">The path of the assembly.</param>
		/// <param name="assembly">The loaded assembly. Null if not successful in loading.</param>
		public static bool TryLoadAssembly(string path, out AssemblyDefinition assembly)
		{
			try
			{
				assembly = AssemblyDefinition.ReadAssembly(path);
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
		/// Performs work to dispose collection objects.
		/// </summary>
		public void Dispose()
		{
			foreach (var assembly in AssembliesToPatch)
				assembly.Value.Dispose();

			AssembliesToPatch.Clear();

			// Clear to allow GC collection.
			PatcherPlugins.Clear();
		}

		/// <summary>
		///     Applies patchers to all assemblies in the given directory and loads patched assemblies into memory.
		/// </summary>
		/// <param name="directory">Directory to load CLR assemblies from.</param>
		public void PatchAndLoad()
		{
			// First, create a copy of the assembly dictionary as the initializer can change them
			var assemblies = new Dictionary<string, AssemblyDefinition>(AssembliesToPatch, StringComparer.InvariantCultureIgnoreCase);

			// Next, initialize all the patchers
			foreach (var assemblyPatcher in PatcherPluginsSafe)
			{
				try
				{
					assemblyPatcher.Initializer?.Invoke();
				}
				catch (Exception ex)
				{
					Logger.LogError($"Failed to run initializer of {assemblyPatcher.TypeName}: {ex}");
				}
			}

			// Then, perform the actual patching

			var patchedAssemblies = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
			var resolvedAssemblies = new Dictionary<string, string>();

			// TODO: Maybe instead reload the assembly and repatch with other valid patchers?
			var invalidAssemblies = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

			foreach (var assemblyPatcher in PatcherPluginsSafe)
				foreach (string targetDll in assemblyPatcher.TargetDLLs())
					if (AssembliesToPatch.TryGetValue(targetDll, out var assembly) && !invalidAssemblies.Contains(targetDll))
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

						AssembliesToPatch[targetDll] = assembly;
						patchedAssemblies.Add(targetDll);

						foreach (var resolvedAss in AppDomain.CurrentDomain.GetAssemblies())
						{
							var name = Utility.TryParseAssemblyName(resolvedAss.FullName, out var assName) ? assName.Name : resolvedAss.FullName;

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
				Logger.LogInfo($"BepInEx is about load the following assemblies:\n{String.Join("\n", patchedAssemblies.ToArray())}");
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
				{
					Assembly loadedAssembly;

					if (ConfigLoadDumpedAssemblies.Value)
						loadedAssembly = Assembly.LoadFile(Path.Combine(DumpedAssembliesPath, filename));
					else
					{
						using (var assemblyStream = new MemoryStream())
						{
							assembly.Write(assemblyStream);
							loadedAssembly =Assembly.Load(assemblyStream.ToArray());
						}
					}

					LoadedAssemblies.Add(filename, loadedAssembly);

					Logger.LogDebug($"Loaded '{assembly.FullName}' into memory");
				}

				// Though we have to dispose of all assemblies regardless of them being patched or not
				assembly.Dispose();
			}

			// Finally, run all finalizers
			foreach (var assemblyPatcher in PatcherPluginsSafe)
			{
				try
				{
					assemblyPatcher.Finalizer?.Invoke();
				}
				catch (Exception ex)
				{
					Logger.LogError($"Failed to run finalizer of {assemblyPatcher.TypeName}: {ex}");
				}
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