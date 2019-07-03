using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Contract;
using Mono.Cecil;
using UnityEngine;
using UnityInjector.ConsoleUtil;
using Logger = BepInEx.Logging.Logger;

namespace BepInEx.Bootstrap
{
	/// <summary>
	/// The manager and loader for all plugins, and the entry point for BepInEx plugin system.
	/// </summary>
	public static class Chainloader
	{
		/// <summary>
		/// The loaded and initialized list of plugins.
		/// </summary>
		public static Dictionary<string, PluginInfo> PluginInfos { get; } = new Dictionary<string, PluginInfo>();

		public static List<BaseUnityPlugin> Plugins { get; } = new List<BaseUnityPlugin>();

		/// <summary>
        /// The GameObject that all plugins are attached to as components.
        /// </summary>
        public static GameObject ManagerObject { get; private set; }


		private static bool _loaded = false;
		private static bool _initialized = false;

		/// <summary>
        /// Initializes BepInEx to be able to start the chainloader.
        /// </summary>
        public static void Initialize(string gameExePath, bool startConsole = true)
		{
			if (_initialized)
				return;

            // Set vitals
			if (gameExePath != null)
			{
				// Checking for null allows a more advanced initialization workflow, where the Paths class has been initialized before calling Chainloader.Initialize
				// This is used by Preloader to use environment variables, for example
				Paths.SetExecutablePath(gameExePath);
			}

            // Start logging
            if (ConsoleWindow.ConfigConsoleEnabled.Value && startConsole)
			{
				ConsoleWindow.Attach();
				Logger.Listeners.Add(new ConsoleLogListener());
            }

			// Fix for standard output getting overwritten by UnityLogger
			if (ConsoleWindow.StandardOut != null)
			{
				Console.SetOut(ConsoleWindow.StandardOut);

				var encoding = ConsoleWindow.ConfigConsoleShiftJis.Value ? 932 : (uint)Encoding.UTF8.CodePage;
				ConsoleEncoding.ConsoleCodePage = encoding;
				Console.OutputEncoding = ConsoleEncoding.GetEncoding(encoding);
			}

            Logger.Listeners.Add(new UnityLogListener());
			Logger.Listeners.Add(new DiskLogListener());

			if (!TraceLogSource.IsListening)
				Logger.Sources.Add(TraceLogSource.CreateSource());

			if (ConfigUnityLogging.Value)
				Logger.Sources.Add(new UnityLogSource());


			Logger.LogMessage("Chainloader ready");

			_initialized = true;
		}

		private static Regex allowedGuidRegex { get; } = new Regex(@"^[a-zA-Z0-9\._]+$");

        public static PluginInfo ToPluginInfo(TypeDefinition type)
		{
			if (type.IsInterface || type.IsAbstract || !type.IsSubtypeOf(typeof(BaseUnityPlugin)))
				return null;

			var metadata = BepInPlugin.FromCecilType(type);

			if (metadata == null)
			{
				Logger.LogWarning($"Skipping over type [{type.FullName}] as no metadata attribute is specified");
				return null;
			}

			if (string.IsNullOrEmpty(metadata.GUID) || !allowedGuidRegex.IsMatch(metadata.GUID))
			{
				Logger.LogWarning($"Skipping type [{type.FullName}] because its GUID [{metadata.GUID}] is of an illegal format.");
				return null;
			}

			if (metadata.Version == null)
			{
				Logger.LogWarning($"Skipping type [{type.FullName}] because its version is invalid.");
				return null;
			}

			if (metadata.Name == null)
			{
				Logger.LogWarning($"Skipping type [{type.FullName}] because its name is null.");
				return null;
			}

            //Perform a filter for currently running process
            var filters = BepInProcess.FromCecilType(type);

			bool invalidProcessName = filters.Any(x => !string.Equals(x.ProcessName.Replace(".exe", ""), Paths.ProcessName, StringComparison.InvariantCultureIgnoreCase));

			if (invalidProcessName)
			{
				Logger.LogWarning($"Skipping over plugin [{metadata.GUID}] due to process filter");
				return null;
			}

			var dependencies = BepInDependency.FromCecilType(type);

			return new PluginInfo
			{
				Metadata = metadata,
				Processes = filters,
				Dependencies = dependencies,
				CecilType = type,
				Location = type.Module.FileName
			};
		}

		private static readonly string CurrentAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;

		private static bool HasBepinPlugins(AssemblyDefinition ass)
		{
			if (ass.MainModule.AssemblyReferences.All(r => r.Name != CurrentAssemblyName))
				return false;
			if (ass.MainModule.GetTypeReferences().All(r => r.FullName != typeof(BaseUnityPlugin).FullName))
				return false;

			return true;
		}

		/// <summary>
		/// The entrypoint for the BepInEx plugin system.
		/// </summary>
		public static void Start()
		{
			if (_loaded)
				return;

			if (!_initialized)
				throw new InvalidOperationException("BepInEx has not been initialized. Please call Chainloader.Initialize prior to starting the chainloader instance.");

			if (!Directory.Exists(Paths.PluginPath))
				Directory.CreateDirectory(Paths.PluginPath);

			if (!Directory.Exists(Paths.PatcherPluginPath))
				Directory.CreateDirectory(Paths.PatcherPluginPath);

			try
			{
				var productNameProp = typeof(Application).GetProperty("productName", BindingFlags.Public | BindingFlags.Static);
				if (productNameProp != null)
					ConsoleWindow.Title = $"BepInEx {Assembly.GetExecutingAssembly().GetName().Version} - {productNameProp.GetValue(null, null)}";

				Logger.LogMessage("Chainloader started");

				ManagerObject = new GameObject("BepInEx_Manager");

				UnityEngine.Object.DontDestroyOnLoad(ManagerObject);

				var pluginsToLoad = TypeLoader.FindPluginTypes(Paths.PluginPath, ToPluginInfo, HasBepinPlugins);

				var pluginInfos = pluginsToLoad.SelectMany(p => p.Value).ToList();

				var loadedAssemblies = new Dictionary<AssemblyDefinition, Assembly>();

				Logger.LogInfo($"{pluginInfos.Count} plugins to load");

				var dependencyDict = new Dictionary<string, IEnumerable<string>>();
				var pluginsByGUID = new Dictionary<string, PluginInfo>();

				foreach (var pluginInfo in pluginInfos)
				{
					if (pluginInfo.Metadata.GUID == null)
					{
						Logger.LogWarning($"Skipping [{pluginInfo.Metadata.Name}] because it does not have a valid GUID.");
						continue;
					}

					if (dependencyDict.ContainsKey(pluginInfo.Metadata.GUID))
					{
						Logger.LogWarning($"Skipping [{pluginInfo.Metadata.Name}] because its GUID ({pluginInfo.Metadata.GUID}) is already used by another plugin.");
						continue;
					}

					dependencyDict[pluginInfo.Metadata.GUID] = pluginInfo.Dependencies.Select(d => d.DependencyGUID);
					pluginsByGUID[pluginInfo.Metadata.GUID] = pluginInfo;
				}

				var emptyDependencies = new string[0];

				// Sort plugins by their dependencies.
				// Give missing dependencies no dependencies of its own, which will cause missing plugins to be first in the resulting list.
				var sortedPlugins = Utility.TopologicalSort(dependencyDict.Keys, x => dependencyDict.TryGetValue(x, out var deps) ? deps : emptyDependencies).ToList();

				var invalidPlugins = new HashSet<string>();
				var processedPlugins = new HashSet<string>();

				foreach (var pluginGUID in sortedPlugins)
				{
					// If the plugin is missing, don't process it
					if (!pluginsByGUID.TryGetValue(pluginGUID, out var pluginInfo))
						continue;

					var dependsOnInvalidPlugin = false;
					var missingDependencies = new List<string>();
					foreach (var dependency in pluginInfo.Dependencies)
					{
						// If the depenency wasn't already processed, it's missing altogether
						if (!processedPlugins.Contains(dependency.DependencyGUID))
						{
							// If the dependency is hard, collect it into a list to show
							if ((dependency.Flags & BepInDependency.DependencyFlags.HardDependency) != 0)
								missingDependencies.Add(dependency.DependencyGUID);
							continue;
						}

						// If the dependency is invalid (e.g. has missing depedencies), report that to the user
						if (invalidPlugins.Contains(dependency.DependencyGUID))
						{
							dependsOnInvalidPlugin = true;
							break;
						}
					}

					processedPlugins.Add(pluginGUID);

					if (dependsOnInvalidPlugin)
					{
						Logger.LogWarning($"Skipping [{pluginInfo.Metadata.Name}] because it has a dependency that was not loaded. See above errors for details.");
						continue;
					}

					if (missingDependencies.Count != 0)
					{
						Logger.LogError($@"Missing the following dependencies for [{pluginInfo.Metadata.Name}]: {"\r\n"}{
								string.Join("\r\n", missingDependencies.Select(s => $"- {s}").ToArray())
							}{"\r\n"}Loading will be skipped; expect further errors and unstabilities.");

						invalidPlugins.Add(pluginGUID);
						continue;
					}

					try
					{
						Logger.LogInfo($"Loading [{pluginInfo.Metadata.Name} {pluginInfo.Metadata.Version}]");

						if (!loadedAssemblies.TryGetValue(pluginInfo.CecilType.Module.Assembly, out var ass))
							loadedAssemblies[pluginInfo.CecilType.Module.Assembly] = ass = Assembly.LoadFile(pluginInfo.Location);

						PluginInfos[pluginGUID] = pluginInfo;
						pluginInfo.Instance = (BaseUnityPlugin)ManagerObject.AddComponent(ass.GetType(pluginInfo.CecilType.FullName));
						pluginInfo.CecilType = null;

						Plugins.Add(pluginInfo.Instance);
					}
					catch (Exception ex)
					{
						invalidPlugins.Add(pluginGUID);
						PluginInfos.Remove(pluginGUID);

						Logger.LogError($"Error loading [{pluginInfo.Metadata.Name}] : {ex.Message}");
						if (ex is ReflectionTypeLoadException re)
							Logger.LogDebug(TypeLoader.TypeLoadExceptionToString(re));
						else
							Logger.LogDebug(ex);
					}
				}

				foreach (var selectedTypesInfo in pluginsToLoad)
				{
					selectedTypesInfo.Key.Dispose();
				}
			}
			catch (Exception ex)
			{
				ConsoleWindow.Attach();

				Console.WriteLine("Error occurred starting the game");
				Console.WriteLine(ex.ToString());
			}

			Logger.LogMessage("Chainloader startup complete");

			_loaded = true;
		}

		#region Config

		private static readonly ConfigWrapper<string> ConfigPluginsDirectory = ConfigFile.CoreConfig.Wrap("Paths", "PluginsDirectory", "The relative directory to the BepInEx folder where plugins are loaded.", "plugins");

		private static readonly ConfigWrapper<bool> ConfigUnityLogging = ConfigFile.CoreConfig.Wrap("Logging", "UnityLogListening", "Enables showing unity log messages in the BepInEx logging system.", true);

		#endregion
	}
}