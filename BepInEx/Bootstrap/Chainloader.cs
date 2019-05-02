using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
		public static Dictionary<string, PluginInfo> Plugins { get; private set; } = new Dictionary<string, PluginInfo>();

		/// <summary>
		/// The GameObject that all plugins are attached to as components.
		/// </summary>
		public static GameObject ManagerObject { get; private set; }


		private static bool _loaded = false;
		private static bool _initialized = false;

		/// <summary>
        /// Initializes BepInEx to be able to start the chainloader.
        /// </summary>
        public static void Initialize(string containerExePath, bool startConsole = true)
		{
			if (_initialized)
				return;

			//Set vitals
			Paths.SetExecutablePath(containerExePath);

			Paths.SetPluginPath(ConfigPluginsDirectory.Value);

            //Start logging
            if (ConsoleWindow.ConfigConsoleEnabled.Value && startConsole)
			{
				ConsoleWindow.Attach();
				Logger.Listeners.Add(new ConsoleLogListener());
            }

			//Fix for standard output getting overwritten by UnityLogger
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

				var pluginsToLoad = TypeLoader.FindPluginTypes(Paths.PluginPath);

				var pluginInfos = pluginsToLoad.SelectMany(p => p.Value).ToList();

				var loadedAssemblies = new Dictionary<AssemblyDefinition, Assembly>();

				Logger.LogInfo($"{pluginInfos.Count} / {pluginInfos.Count} plugins to load");

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

						Plugins[pluginGUID] = pluginInfo;
						pluginInfo.Instance = (BaseUnityPlugin)ManagerObject.AddComponent(ass.GetType(pluginInfo.CecilType.FullName));
						pluginInfo.CecilType = null;
					}
					catch (Exception ex)
					{
						invalidPlugins.Add(pluginGUID);
						Plugins.Remove(pluginGUID);

						Logger.LogError($"Error loading [{pluginInfo.Metadata.Name}] : {ex.Message}");
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