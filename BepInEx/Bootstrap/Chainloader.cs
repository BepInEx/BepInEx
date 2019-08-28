using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
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

		public static List<string> DependencyErrors { get; } = new List<string>();

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

			if (ConfigDiskLogging.Value)
			{
				var logLevel = (LogLevel)Enum.Parse(typeof(LogLevel), ConfigDiskConsoleDisplayedLevel.Value, true);
				Logger.Listeners.Add(new DiskLogListener("LogOutput.log", logLevel, ConfigDiskAppend.Value, ConfigDiskWriteUnityLog.Value));
			}

			if (!TraceLogSource.IsListening)
				Logger.Sources.Add(TraceLogSource.CreateSource());

			if (ConfigUnityLogging.Value)
				Logger.Sources.Add(new UnityLogSource());


			Logger.LogMessage("Chainloader ready");

			_initialized = true;
		}

		private static Regex allowedGuidRegex { get; } = new Regex(@"^[a-zA-Z0-9\._\-]+$");

		public static PluginInfo ToPluginInfo(TypeDefinition type)
		{
			if (type.IsInterface || type.IsAbstract || !type.IsSubtypeOf(typeof(BaseUnityPlugin)))
				return null;

			var metadata = BepInPlugin.FromCecilType(type);

			// Perform checks that will prevent the plugin from being loaded in ALL cases
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

			var filters = BepInProcess.FromCecilType(type);
			var dependencies = BepInDependency.FromCecilType(type);
			var incompatibilities = BepInIncompatibility.FromCecilType(type);

			return new PluginInfo
			{
				Metadata = metadata,
				Processes = filters,
				Dependencies = dependencies,
				Incompatibilities = incompatibilities,
				TypeName = type.FullName
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

				var pluginsToLoad = TypeLoader.FindPluginTypes(Paths.PluginPath, ToPluginInfo, HasBepinPlugins, "chainloader");
				foreach (var keyValuePair in pluginsToLoad)
					foreach (var pluginInfo in keyValuePair.Value)
						pluginInfo.Location = keyValuePair.Key;
				var pluginInfos = pluginsToLoad.SelectMany(p => p.Value).ToList();
				var loadedAssemblies = new Dictionary<string, Assembly>();

				Logger.LogInfo($"{pluginInfos.Count} plugins to load");

				var dependencyDict = new Dictionary<string, IEnumerable<string>>();
				var pluginsByGUID = new Dictionary<string, PluginInfo>();

				foreach (var pluginInfo in pluginInfos)
				{
					// Perform checks that will prevent loading plugins in this run
					var filters = pluginInfo.Processes.ToList();
					bool invalidProcessName = filters.Count != 0 && filters.All(x => !string.Equals(x.ProcessName.Replace(".exe", ""), Paths.ProcessName, StringComparison.InvariantCultureIgnoreCase));

					if (invalidProcessName)
					{
						Logger.LogWarning($"Skipping over plugin [{pluginInfo.Metadata.GUID}] due to process filter");
						continue;
					}

					if (dependencyDict.ContainsKey(pluginInfo.Metadata.GUID))
					{
						Logger.LogWarning($"Skipping [{pluginInfo.Metadata.Name}] because its GUID ({pluginInfo.Metadata.GUID}) is already used by another plugin.");
						continue;
					}

					dependencyDict[pluginInfo.Metadata.GUID] = pluginInfo.Dependencies.Select(d => d.DependencyGUID).Concat(pluginInfo.Incompatibilities.Select(i => i.IncompatibilityGUID));
					pluginsByGUID[pluginInfo.Metadata.GUID] = pluginInfo;
				}

				var emptyDependencies = new string[0];

				// Sort plugins by their dependencies.
				// Give missing dependencies no dependencies of its own, which will cause missing plugins to be first in the resulting list.
				var sortedPlugins = Utility.TopologicalSort(dependencyDict.Keys, x => dependencyDict.TryGetValue(x, out var deps) ? deps : emptyDependencies).ToList();

				var invalidPlugins = new HashSet<string>();
				var processedPlugins = new Dictionary<string, Version>();

				foreach (var pluginGUID in sortedPlugins)
				{
					// If the plugin is missing, don't process it
					if (!pluginsByGUID.TryGetValue(pluginGUID, out var pluginInfo))
						continue;

					var dependsOnInvalidPlugin = false;
					var missingDependencies = new List<BepInDependency>();
					foreach (var dependency in pluginInfo.Dependencies)
					{
						// If the depenency wasn't already processed, it's missing altogether
						bool depenencyExists = processedPlugins.TryGetValue(dependency.DependencyGUID, out var pluginVersion);
						if (!depenencyExists || pluginVersion < dependency.MinimumVersion)
						{
							// If the dependency is hard, collect it into a list to show
							if ((dependency.Flags & BepInDependency.DependencyFlags.HardDependency) != 0)
								missingDependencies.Add(dependency);
							continue;
						}

						// If the dependency is invalid (e.g. has missing depedencies), report that to the user
						if (invalidPlugins.Contains(dependency.DependencyGUID))
						{
							dependsOnInvalidPlugin = true;
							break;
						}
					}

					var incompatibilities = new List<BepInIncompatibility>();
					foreach (var incompatibility in pluginInfo.Incompatibilities)
					{
						if (processedPlugins.ContainsKey(incompatibility.IncompatibilityGUID))
							incompatibilities.Add(incompatibility);
					}

					processedPlugins.Add(pluginGUID, pluginInfo.Metadata.Version);

					if (dependsOnInvalidPlugin)
					{
						string message = $"Skipping [{pluginInfo.Metadata.Name}] because it has a dependency that was not loaded. See above errors for details.";
						DependencyErrors.Add(message);
						Logger.LogWarning(message);
						continue;
					}

					if (missingDependencies.Count != 0)
					{
						string ToMissingString(BepInDependency s)
						{
							if (s.MinimumVersion.IsZero()) return "- " + s.DependencyGUID;
							return $"- {s.DependencyGUID} (at least v{s.MinimumVersion})";
						}

						string message = $@"Could not load [{pluginInfo.Metadata.Name}] because it has missing dependencies: {string.Join(", ", missingDependencies.Select(ToMissingString).ToArray())}";
						DependencyErrors.Add(message);
						Logger.LogError(message);

						invalidPlugins.Add(pluginGUID);
						continue;
					}

					if (incompatibilities.Count != 0)
					{
						string message = $@"Could not load [{pluginInfo.Metadata.Name}] because it is incompatible with: {string.Join(", ", incompatibilities.Select(i => i.IncompatibilityGUID).ToArray())}";
						DependencyErrors.Add(message);
						Logger.LogError(message);

						invalidPlugins.Add(pluginGUID);
						continue;
					}

					try
					{
						Logger.LogInfo($"Loading [{pluginInfo.Metadata.Name} {pluginInfo.Metadata.Version}]");

						if (!loadedAssemblies.TryGetValue(pluginInfo.Location, out var ass))
							loadedAssemblies[pluginInfo.Location] = ass = Assembly.LoadFile(pluginInfo.Location);

						PluginInfos[pluginGUID] = pluginInfo;
						pluginInfo.Instance = (BaseUnityPlugin)ManagerObject.AddComponent(ass.GetType(pluginInfo.TypeName));

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

		private static readonly ConfigWrapper<bool> ConfigUnityLogging = ConfigFile.CoreConfig.Wrap(
			"Logging",
			"UnityLogListening",
			"Enables showing unity log messages in the BepInEx logging system.",
			true);

		private static readonly ConfigWrapper<bool> ConfigDiskLogging = ConfigFile.CoreConfig.Wrap(
			"Logging.Disk",
			"Enabled",
			"Enables writing log messages to disk.",
			true);

		private static readonly ConfigWrapper<string> ConfigDiskConsoleDisplayedLevel = ConfigFile.CoreConfig.Wrap(
			"Logging.Disk",
			"DisplayedLogLevel",
			"Only displays the specified log level and above in the console output.",
			"Info");

		private static readonly ConfigWrapper<bool> ConfigDiskWriteUnityLog = ConfigFile.CoreConfig.Wrap(
			"Logging.Disk",
			"WriteUnityLog",
			"Include unity log messages in log file output.",
			false);

		private static readonly ConfigWrapper<bool> ConfigDiskAppend = ConfigFile.CoreConfig.Wrap(
			"Logging.Disk",
			"AppendLog",
			"Appends to the log file instead of overwriting, on game startup.",
			false);
		#endregion
	}
}