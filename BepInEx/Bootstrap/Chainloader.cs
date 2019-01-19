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
	public static class Chainloader
	{
		/// <summary>
		/// The loaded and initialized list of plugins.
		/// </summary>
		public static List<BaseUnityPlugin> Plugins { get; private set; } = new List<BaseUnityPlugin>();

		/// <summary>
		/// The GameObject that all plugins are attached to as components.
		/// </summary>
		public static GameObject ManagerObject { get; private set; } = new GameObject("BepInEx_Manager");


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

				Dictionary<Type, IEnumerable<Type>> dependencyDict = new Dictionary<Type, IEnumerable<Type>>();


				foreach (Type t in pluginTypes)
				{
					try
					{
						IEnumerable<Type> dependencies = MetadataHelper.GetDependencies(t, pluginTypes);

						dependencyDict[t] = dependencies;
					}
					catch (MissingDependencyException)
					{
						var metadata = MetadataHelper.GetMetadata(t);

						Logger.Log(LogLevel.Info, $"Cannot load [{metadata.Name}] due to missing dependencies.");
					}
				}

				pluginTypes = Utility.TopologicalSort(dependencyDict.Keys, x => dependencyDict[x]).ToList();

				foreach (Type t in pluginTypes)
				{
					try
					{
						var metadata = MetadataHelper.GetMetadata(t);

						var plugin = (BaseUnityPlugin) ManagerObject.AddComponent(t);

						Plugins.Add(plugin);
						Logger.Log(LogLevel.Info, $"Loaded [{metadata.Name} {metadata.Version}]");
					}
					catch (Exception ex)
					{
						Logger.Log(LogLevel.Info, $"Error loading [{t.Name}] : {ex.Message}");
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