using BepInEx.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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

		/// <summary>
		/// The entry point for the BepInEx plugin system, called on the very first LoadScene() from UnityEngine.
		/// </summary>
		public static void Initialize()
		{
			if (_loaded)
				return;

		    if (!Directory.Exists(Utility.PluginsDirectory))
		        Directory.CreateDirectory(Utility.PluginsDirectory);

            Preloader.AllocateConsole();

			try
			{
                UnityLogWriter unityLogWriter = new UnityLogWriter();

			    if (Preloader.PreloaderLog != null)
			        unityLogWriter.WriteToLog($"{Preloader.PreloaderLog}\r\n");

                Logger.SetLogger(unityLogWriter);

                if(bool.Parse(Config.GetEntry("log_unity_messages", "false", "Global")))
                    UnityLogWriter.ListenUnityLogs();

			    string consoleTile = $"BepInEx {Assembly.GetExecutingAssembly().GetName().Version} - {Application.productName}";
			    ConsoleWindow.Title = consoleTile;
                
				Logger.Log(LogLevel.Message, "Chainloader started");

				UnityEngine.Object.DontDestroyOnLoad(ManagerObject);


			    string currentProcess = Process.GetCurrentProcess().ProcessName.ToLower();

				var pluginTypes = TypeLoader.LoadTypes<BaseUnityPlugin>(Utility.PluginsDirectory)
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
				UnityInjector.ConsoleUtil.ConsoleWindow.Attach();

				Console.WriteLine("Error occurred starting the game");
				Console.WriteLine(ex.ToString());
			}

			_loaded = true;
		}
	}
}