using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using UnityEngine;
using UnityInjector.ConsoleUtil;
using UnityLogWriter = BepInEx.Logging.UnityLogWriter;

namespace BepInEx.Bootstrap
{
	/// <summary>
	/// The manager and loader for all plugins, and the entry point for BepInEx plugin system.
	/// </summary>
	public class Chainloader
	{
		/// <summary>
		/// The loaded and initialized list of plugins.
		/// </summary>
		public static List<BaseUnityPlugin> Plugins { get; protected set; } = new List<BaseUnityPlugin>();

		/// <summary>
		/// The GameObject that all plugins are attached to as components.
		/// </summary>
		public static GameObject ManagerObject { get; protected set; } = new GameObject("BepInEx_Manager");


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
			Paths.ExecutablePath = containerExePath;

			//Start logging

			if (startConsole)
			{
				ConsoleWindow.Attach();
				
				ConsoleEncoding.ConsoleCodePage = (uint)Encoding.UTF8.CodePage;
				Console.OutputEncoding = Encoding.UTF8;
			}
			
			UnityLogWriter unityLogWriter = new UnityLogWriter();

			if (Preloader.PreloaderLog != null)
				unityLogWriter.WriteToLog($"{Preloader.PreloaderLog}\r\n");

			Logger.SetLogger(unityLogWriter);


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

			try
			{
				if (bool.Parse(Config.GetEntry("chainloader-log-unity-messages", "false", "BepInEx")))
					UnityLogWriter.ListenUnityLogs();

				var productNameProp = typeof(Application).GetProperty("productName", BindingFlags.Public | BindingFlags.Static);
				if (productNameProp != null)
					ConsoleWindow.Title =
						$"BepInEx {Assembly.GetExecutingAssembly().GetName().Version} - {productNameProp.GetValue(null, null)}";

				Logger.Log(LogLevel.Message, "Chainloader started");

				UnityEngine.Object.DontDestroyOnLoad(ManagerObject);


				string currentProcess = Process.GetCurrentProcess().ProcessName.ToLower();

				var pluginTypes = TypeLoader.LoadTypes<BaseUnityPlugin>(Paths.PluginPath)
					.Where(plugin =>
					{
						//Perform a filter for currently running process
						var filters = MetadataHelper.GetAttributes<BepInProcess>(plugin);

						if (!filters.Any())
							return true;

						return filters.Any(x => x.ProcessName.ToLower().Replace(".exe", "") == currentProcess);
					})
					.ToList();

				Logger.Log(LogLevel.Info, $"{pluginTypes.Count} plugins selected");

				var dependencyDict = new Dictionary<string, IEnumerable<string>>();
				var pluginsByGUID = new Dictionary<string, Type>();

				foreach (Type t in pluginTypes)
				{
					var dependencies = MetadataHelper.GetDependencies(t, pluginTypes);
					var metadata = MetadataHelper.GetMetadata(t);

					if (metadata.GUID == null)
					{
						Logger.Log(LogLevel.Warning, $"Skipping [{metadata.Name}] because it does not have a valid GUID.");
						continue;
					}

					if (dependencyDict.ContainsKey(metadata.GUID))
					{
						Logger.Log(LogLevel.Warning, $"Skipping [{metadata.Name}] because its GUID ({metadata.GUID}) is already used by another plugin.");
						continue;
					}

					dependencyDict[metadata.GUID] = dependencies.Select(d => d.DependencyGUID);
					pluginsByGUID[metadata.GUID] = t;
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
					if (!pluginsByGUID.TryGetValue(pluginGUID, out var pluginType))
						continue;

					var metadata = MetadataHelper.GetMetadata(pluginType);
					var dependencies = MetadataHelper.GetDependencies(pluginType, pluginTypes);
					var dependsOnInvalidPlugin = false;
					var missingDependencies = new List<string>();
					foreach (var dependency in dependencies)
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
						Logger.Log(LogLevel.Warning, $"Skipping [{metadata.Name}] because it has a dependency that was not loaded. See above errors for details.");
						continue;
					}

					if (missingDependencies.Count != 0)
					{
						Logger.Log(LogLevel.Error, $@"Missing the following dependencies for [{metadata.Name}]: {"\r\n"}{
							string.Join("\r\n", missingDependencies.Select(s => $"- {s}").ToArray())
							}{"\r\n"}Loading will be skipped; expect further errors and unstabilities.");

						invalidPlugins.Add(pluginGUID);
						continue;
					}

					try
					{
						Plugins.Add((BaseUnityPlugin)ManagerObject.AddComponent(pluginType));
						Logger.Log(LogLevel.Info, $"Loaded [{metadata.Name} {metadata.Version}]");
					}
					catch (Exception ex)
					{
						invalidPlugins.Add(pluginGUID);
						Logger.Log(LogLevel.Info, $"Error loading [{metadata.Name}] : {ex.Message}");
					}
				}
			}
			catch (Exception ex)
			{
				ConsoleWindow.Attach();

				Console.WriteLine("Error occurred starting the game");
				Console.WriteLine(ex.ToString());
			}

			_loaded = true;
		}
	}
}