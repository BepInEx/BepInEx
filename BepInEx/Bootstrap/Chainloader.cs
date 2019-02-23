using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using BepInEx.Logging;
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
		public static List<BaseUnityPlugin> Plugins { get; private set; } = new List<BaseUnityPlugin>();

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

			if (startConsole)
			{
				ConsoleWindow.Attach();

				ConsoleEncoding.ConsoleCodePage = (uint)Encoding.UTF8.CodePage;
				Console.OutputEncoding = Encoding.UTF8;
				Logger.Listeners.Add(new ConsoleLogListener());
			}

			//Fix for standard output getting overwritten by UnityLogger
			Console.SetOut(ConsoleWindow.StandardOut);


			Logger.Listeners.Add(new UnityLogListener());

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
					ConsoleWindow.Title =
						$"BepInEx {Assembly.GetExecutingAssembly().GetName().Version} - {productNameProp.GetValue(null, null)}";

				Logger.LogMessage("Chainloader started");

				ManagerObject = new GameObject("BepInEx_Manager");

				UnityEngine.Object.DontDestroyOnLoad(ManagerObject);


				string currentProcess = Process.GetCurrentProcess().ProcessName.ToLower();

				var globalPluginTypes = TypeLoader.LoadTypes<BaseUnityPlugin>(Paths.PluginPath).ToList();

				var selectedPluginTypes = globalPluginTypes
										  .Where(plugin =>
										  {
											  //Ensure metadata exists
											  var metadata = MetadataHelper.GetMetadata(plugin);

											  if (metadata == null)
											  {
												  Logger.LogWarning($"Skipping over type [{plugin.Name}] as no metadata attribute is specified");
												  return false;
											  }

											  //Perform a filter for currently running process
											  var filters = MetadataHelper.GetAttributes<BepInProcess>(plugin);

											  if (filters.Length == 0) //no filters means it loads everywhere
												  return true;

											  var result = filters.Any(x => x.ProcessName.ToLower().Replace(".exe", "") == currentProcess);

											  if (!result)
												  Logger.LogInfo($"Skipping over plugin [{metadata.GUID}] due to process filter");

											  return result;
										  })
										  .ToList();

				Logger.LogInfo($"{selectedPluginTypes.Count} / {globalPluginTypes.Count} plugins to load");

				Dictionary<Type, IEnumerable<Type>> dependencyDict = new Dictionary<Type, IEnumerable<Type>>();


				foreach (Type t in selectedPluginTypes)
				{
					try
					{
						IEnumerable<Type> dependencies = MetadataHelper.GetDependencies(t, selectedPluginTypes);

						dependencyDict[t] = dependencies;
					}
					catch (MissingDependencyException)
					{
						var metadata = MetadataHelper.GetMetadata(t);

						Logger.LogWarning($"Cannot load [{metadata.Name}] due to missing dependencies.");
					}
				}

				var sortedTypes = Utility.TopologicalSort(dependencyDict.Keys, x => dependencyDict[x]).ToList();

				foreach (Type t in sortedTypes)
				{
					try
					{
						var metadata = MetadataHelper.GetMetadata(t);
						Logger.LogInfo($"Loading [{metadata.Name} {metadata.Version}]");

						var plugin = (BaseUnityPlugin)ManagerObject.AddComponent(t);

						Plugins.Add(plugin);
					}
					catch (Exception ex)
					{
						Logger.LogError($"Error loading [{t.Name}] : {ex.Message}");
						Logger.LogDebug(ex);
					}
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

		private static ConfigWrapper<string> ConfigPluginsDirectory = ConfigFile.CoreConfig.Wrap(
				"Paths",
				"PluginsDirectory",
				"The relative directory to the BepInEx folder where plugins are loaded.",
				"plugins");

		private static ConfigWrapper<bool> ConfigUnityLogging = ConfigFile.CoreConfig.Wrap(
				"Logging",
				"UnityLogListening",
				"Enables showing unity log messages in the BepInEx logging system.",
				true);

		#endregion
	}
}