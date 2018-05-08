using BepInEx.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BepInEx
{
	/// <summary>
	/// The manager and loader for all plugins, and the entry point for BepInEx.
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


		static bool loaded = false;

		/// <summary>
		/// The entry point for BepInEx, called on the very first LoadScene() from UnityEngine.
		/// </summary>
		public static void Initialize()
		{
			if (loaded)
				return;

		    if (!Directory.Exists(Common.Utility.PluginsDirectory))
		        Directory.CreateDirectory(Utility.PluginsDirectory);

			if (bool.Parse(Config.GetEntry("console", "false")) || bool.Parse(Config.GetEntry("console-shiftjis", "false")))
			{
				try
				{
					UnityInjector.ConsoleUtil.ConsoleWindow.Attach();

					if (bool.Parse(Config.GetEntry("console-shiftjis", "false")))
						UnityInjector.ConsoleUtil.ConsoleEncoding.ConsoleCodePage = 932;
				}
				catch
				{
					BepInLogger.Log("Failed to allocate console!", true);
				}
			}

			try
			{
				BepInLogger.Log($"BepInEx {Assembly.GetExecutingAssembly().GetName().Version}");
				BepInLogger.Log("Chainloader started");

				UnityEngine.Object.DontDestroyOnLoad(ManagerObject);


			    string currentProcess = Process.GetCurrentProcess().ProcessName.ToLower();

				var pluginTypes = TypeLoader.LoadTypes<BaseUnityPlugin>(Utility.PluginsDirectory)
				    .Where(plugin =>
				    {
                        //Perform a filter for currently running process
				        var filters = TypeLoader.GetAttributes<BepInProcess>(plugin);

				        if (!filters.Any())
				            return true;

				        return filters.Any(x => x.ProcessName.ToLower().Replace(".exe", "") == currentProcess);
				    })
				    .ToList();

				BepInLogger.Log($"{pluginTypes.Count} plugins selected");

				Dictionary<Type, IEnumerable<Type>> dependencyDict = new Dictionary<Type, IEnumerable<Type>>();

				foreach (Type t in pluginTypes)
				{
					try
					{
						IEnumerable<Type> dependencies = TypeLoader.GetDependencies(t, pluginTypes);

						dependencyDict[t] = dependencies;
					}
					catch (MissingDependencyException)
					{
						var metadata = TypeLoader.GetMetadata(t);

						BepInLogger.Log($"Cannot load [{metadata.Name}] due to missing dependencies.");
					}
				}

				pluginTypes = TopologicalSort(dependencyDict.Keys, x => dependencyDict[x]).ToList();

				foreach (Type t in pluginTypes)
				{
					try
					{
						var metadata = TypeLoader.GetMetadata(t);

						var plugin = (BaseUnityPlugin) ManagerObject.AddComponent(t);

						Plugins.Add(plugin);
						BepInLogger.Log($"Loaded [{metadata.Name} {metadata.Version}]");
					}
					catch (Exception ex)
					{
						BepInLogger.Log($"Error loading [{t.Name}] : {ex.Message}");
					}
				}
			}
			catch (Exception ex)
			{
				UnityInjector.ConsoleUtil.ConsoleWindow.Attach();

				Console.WriteLine("Error occurred starting the game");
				Console.WriteLine(ex.ToString());
			}

			loaded = true;
		}

		protected static IEnumerable<TNode> TopologicalSort<TNode>(
			IEnumerable<TNode> nodes,
			Func<TNode, IEnumerable<TNode>> dependencySelector)
		{

			List<TNode> sorted_list = new List<TNode>();

			HashSet<TNode> visited = new HashSet<TNode>();
			HashSet<TNode> sorted = new HashSet<TNode>();

			foreach (TNode input in nodes)
				Visit(input);

			return sorted_list;

			void Visit(TNode node)
			{
				if (visited.Contains(node))
				{
					if (!sorted.Contains(node))
						throw new Exception("Cyclic Dependency");
				}
				else
				{
					visited.Add(node);

					foreach (var dep in dependencySelector(node))
						Visit(dep);

					sorted.Add(node);
					sorted_list.Add(node);
				}
			}
		}
	}
}