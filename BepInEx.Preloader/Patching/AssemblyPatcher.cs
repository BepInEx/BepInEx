using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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
	internal delegate void AssemblyPatcherDelegate(ref AssemblyDefinition assembly);

	/// <summary>
	///     Worker class which is used for loading and patching entire folders of assemblies, or alternatively patching and
	///     loading assemblies one at a time.
	/// </summary>
	internal static class AssemblyPatcher
	{
		private const BindingFlags ALL = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.IgnoreCase;

		public static List<LegacyPatcherPlugin> LegacyPatcherPlugins { get; } = new List<LegacyPatcherPlugin>();
		public static List<PatcherDefinition> PatcherPlugins { get; } = new List<PatcherDefinition>();

		private static readonly string DumpedAssembliesPath = Path.Combine(Paths.BepInExRootPath, "DumpedAssemblies");

		private static T CreateDelegate<T>(MethodInfo method) where T : class => method != null ? Delegate.CreateDelegate(typeof(T), method) as T : null;

		private static LegacyPatcherPlugin ToLegacyPatcherPlugin(TypeDefinition type)
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

			return new LegacyPatcherPlugin
			{
				TypeName = type.FullName
			};
		}

		private static Regex allowedGuidRegex { get; } = new Regex(@"^[a-zA-Z0-9\._\-]+$");
		private static PatcherDefinition ToPatcherPlugin(TypeDefinition type)
		{
			if (type.IsInterface || type.IsAbstract)
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

			return new PatcherDefinition
			{
				TypeName = type.FullName
			};
		}

		/// <summary>
		///     Adds all patchers from all managed assemblies specified in a directory.
		/// </summary>
		/// <param name="directory">Directory to search patcher DLLs from.</param>
		/// <param name="patcherLocator">A function that locates assembly patchers in a given managed assembly.</param>
		public static void AddPatchersFromDirectory(string directory)
		{
			if (!Directory.Exists(directory))
				return;

			var legacyPatchers = TypeLoader.FindPluginTypes(directory, ToLegacyPatcherPlugin);
			var patchers = TypeLoader.FindPluginTypesCacheless(directory, ToPatcherPlugin);

			foreach (var keyValuePair in legacyPatchers)
			{
				var assemblyPath = keyValuePair.Key;
				var patcherCollection = keyValuePair.Value;

				if (patcherCollection.Count == 0)
					continue;

				var assembly = Assembly.LoadFile(assemblyPath);

				foreach (var patcherPlugin in patcherCollection)
				{
					try
					{
						var type = assembly.GetType(patcherPlugin.TypeName);

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

						LegacyPatcherPlugins.Add(patcherPlugin);
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
					$"Loaded {patcherCollection.Count} legacy patcher methods from {assembly.GetName().FullName}");
			}

			foreach (var keyValuePair in patchers)
			{
				var assemblyPath = keyValuePair.Key;
				var patcherCollection = keyValuePair.Value;

				if (patcherCollection.Count == 0)
					continue;

				var assembly = Assembly.LoadFile(assemblyPath);

				foreach (var patcherPlugin in patcherCollection)
				{
					try
					{
						var type = assembly.GetType(patcherPlugin.TypeName);

						patcherPlugin.PatcherInfo = (PatcherInfo)type.GetCustomAttributes(typeof(PatcherInfo), false).FirstOrDefault();

						if (patcherPlugin.PatcherInfo == null)
						{
							Logger.LogWarning($"Skipping over type [{type.FullName}] as no metadata attribute is specified");
							continue;
						}

						if (string.IsNullOrEmpty(patcherPlugin.PatcherInfo.GUID) || !allowedGuidRegex.IsMatch(patcherPlugin.PatcherInfo.GUID))
						{
							Logger.LogWarning($"Skipping type [{type.FullName}] because its GUID [{patcherPlugin.PatcherInfo.GUID}] is of an illegal format.");
							continue;
						}

						if (patcherPlugin.PatcherInfo.Version == null)
						{
							Logger.LogWarning($"Skipping type [{type.FullName}] because its version is invalid.");
							continue;
						}

						if (patcherPlugin.PatcherInfo.Name == null)
						{
							Logger.LogWarning($"Skipping type [{type.FullName}] because its name is null.");
							continue;
						}

						var methods = type.GetMethods(ALL);

						foreach (var method in methods)
						{
							var parameters = method.GetParameters();

							if (parameters.Length == 1 && parameters[0].ParameterType == typeof(AssemblyDefinition))
							{
								var targetAssemblyAttributes = method.GetCustomAttributes(typeof(TargetAssemblyAttribute), false);

								foreach (var attribute in targetAssemblyAttributes)
								{
									patcherPlugin.AssemblyDefinitionPatchers[(TargetAssemblyAttribute)attribute] = method;
								}
							}
							else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(TypeDefinition))
							{
								var targetClassAttributes = method.GetCustomAttributes(typeof(TargetClassAttribute), false);

								foreach (var attribute in targetClassAttributes)
								{
									patcherPlugin.TypeDefinitionPatchers[(TargetClassAttribute)attribute] = method;
								}
							}
						}

						patcherPlugin.Instance = (BasePatcher)Activator.CreateInstance(type);

						Logger.LogInfo($"Loaded patcher [{patcherPlugin.PatcherInfo.Name} {patcherPlugin.PatcherInfo.Version}]");

						PatcherPlugins.Add(patcherPlugin);
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
			}
		}

		private static void InitializePatchers(PatchContext context)
		{
			foreach (var assemblyPatcher in LegacyPatcherPlugins)
				assemblyPatcher.Initializer?.Invoke();

			foreach (var patcher in PatcherPlugins)
				patcher.Instance.Setup(context);
		}

		private static void FinalizePatching()
		{
			foreach (var assemblyPatcher in LegacyPatcherPlugins)
				assemblyPatcher.Finalizer?.Invoke();

			foreach (var patcher in PatcherPlugins)
				patcher.Instance.Finalize();
		}

		/// <summary>
		///     Releases all patchers to let them be collected by GC.
		/// </summary>
		public static void DisposePatchers()
		{
			LegacyPatcherPlugins.Clear();
		}

		/// <summary>
		///     Applies patchers to all assemblies in the given directory and loads patched assemblies into memory.
		/// </summary>
		/// <param name="directory">Directory to load CLR assemblies from.</param>
		public static void PatchAndLoad(string directory)
		{
			// First, load patchable assemblies into Cecil
			var assemblies = new List<AssemblyDefinition>();
			var filenameDictionary = new Dictionary<string, AssemblyDefinition>();

			foreach (string assemblyPath in Directory.GetFiles(directory, "*.dll"))
			{
				var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);

				//NOTE: this is special cased here because the dependency handling for System.dll is a bit wonky
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
				
				assemblies.Add(assembly);
				filenameDictionary[Path.GetFileName(assemblyPath)] = assembly;
				UnityPatches.AssemblyLocations.Add(assembly.FullName, Path.GetFullPath(assemblyPath));
			}

			// Create the patch context
			PatchContext context = new PatchContext
			{
				LoadedPatchers = PatcherPlugins.AsReadOnly(),
				AvailableAssemblies = assemblies,
				AssemblyLocations = filenameDictionary
			};

			// Next, initialize all the patchers
			InitializePatchers(context);

			// Then, perform the actual patching

			// Legacy patchers
			var patchedAssemblies = new HashSet<string>();
			foreach (var assemblyPatcher in LegacyPatcherPlugins)
				foreach (string targetDll in assemblyPatcher.TargetDLLs())
					if (filenameDictionary.TryGetValue(targetDll, out var assembly))
					{
						if (AppDomain.CurrentDomain.GetAssemblies().Any(x => x.GetName().Name == assembly.Name.Name))
							Logger.LogWarning($"Trying to patch an already loaded assembly [{assembly.Name.Name}] with [{assemblyPatcher.TypeName}]");

						Logger.LogInfo($"Patching [{assembly.Name.Name}] with [{assemblyPatcher.TypeName}]");

						var oldAssembly = assembly;

						assemblyPatcher.Patcher?.Invoke(ref assembly);

						if (!ReferenceEquals(oldAssembly, assembly))
						{
							assemblies.Remove(oldAssembly);
							assemblies.Add(assembly);
						}

						filenameDictionary[targetDll] = assembly;
						patchedAssemblies.Add(targetDll);
					}

			// V2 patchers

			foreach (var patcherDefinition in PatcherPlugins)
			{
				patcherDefinition.Instance.PatchAll(assemblies);
			}

			foreach (var patcherDefinition in PatcherPlugins)
			{
				if (patcherDefinition.AssemblyDefinitionPatchers != null)
					foreach (var assemblyPatchDefinition in patcherDefinition.AssemblyDefinitionPatchers)
					{
						var assemblyDefinition = assemblies.FirstOrDefault(x => x.Name.Name == assemblyPatchDefinition.Key.AssemblyName);
						patchedAssemblies.Add(filenameDictionary.First(x => x.Value == assemblyDefinition).Key);

						assemblyPatchDefinition.Value.Invoke(patcherDefinition.Instance, new object[] { assemblyDefinition });
					}
			}

			foreach (var patcherDefinition in PatcherPlugins)
			{
				if (patcherDefinition.TypeDefinitionPatchers != null)
					foreach (var typePatchDefinition in patcherDefinition.TypeDefinitionPatchers)
					{
						var assemblyDefinition = assemblies.FirstOrDefault(x => x.Name.Name == typePatchDefinition.Key.AssemblyName);
						patchedAssemblies.Add(filenameDictionary.First(x => x.Value == assemblyDefinition).Key);

						var typeDefinition = assemblyDefinition?.MainModule.Types.FirstOrDefault(x => x.FullName == typePatchDefinition.Key.ClassName);

						typePatchDefinition.Value.Invoke(patcherDefinition.Instance, new object[] { typeDefinition });
					}
			}

			// Finally, load patched assemblies into memory
			if (ConfigDumpAssemblies.Value || ConfigLoadDumpedAssemblies.Value)
			{
				if (!Directory.Exists(DumpedAssembliesPath))
					Directory.CreateDirectory(DumpedAssembliesPath);

				foreach (KeyValuePair<string, AssemblyDefinition> kv in filenameDictionary)
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

			foreach (var kv in filenameDictionary)
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