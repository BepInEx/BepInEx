using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Mono.Cecil;
using MonoMod.Utils;
using UnityEngine;
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

		private static readonly List<BaseUnityPlugin> _plugins = new List<BaseUnityPlugin>();
		[Obsolete("Use PluginInfos instead")]
		public static List<BaseUnityPlugin> Plugins
		{
			get
			{
				lock (_plugins)
				{
					_plugins.RemoveAll(x => x == null);
					return _plugins.ToList();
				}
			}
		}

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
		public static void Initialize(string gameExePath, bool startConsole = true, ICollection<LogEventArgs> preloaderLogEvents = null)
		{
			if (_initialized)
				return;

			ThreadingHelper.Initialize();

			// Set vitals
			if (gameExePath != null)
			{
				// Checking for null allows a more advanced initialization workflow, where the Paths class has been initialized before calling Chainloader.Initialize
				// This is used by Preloader to use environment variables, for example
				Paths.SetExecutablePath(gameExePath);
			}

			// Start logging
			if (ConsoleManager.ConfigConsoleEnabled.Value && startConsole)
			{
				ConsoleManager.CreateConsole();
				Logger.Listeners.Add(new ConsoleLogListener());
			}

			Logger.InitializeInternalLoggers();
			Logger.Listeners.Add(new UnityLogListener());

			if (ConfigDiskLogging.Value)
				Logger.Listeners.Add(new DiskLogListener("LogOutput.log", ConfigDiskConsoleDisplayedLevel.Value, ConfigDiskAppend.Value, ConfigDiskWriteUnityLog.Value));

			if (!TraceLogSource.IsListening)
				Logger.Sources.Add(TraceLogSource.CreateSource());

			if (ConfigUnityLogging.Value)
				Logger.Sources.Add(new UnityLogSource());


			// Temporarily disable the console log listener as we replay the preloader logs
			var logListener = Logger.Listeners.FirstOrDefault(logger => logger is ConsoleLogListener);

			if (logListener != null)
				Logger.Listeners.Remove(logListener);

			// Write preloader log events if there are any, including the original log source name
			if (preloaderLogEvents != null)
			{
				var preloaderLogSource = Logger.CreateLogSource("Preloader");

				foreach (var preloaderLogEvent in preloaderLogEvents)
					Logger.InternalLogEvent(preloaderLogSource, preloaderLogEvent);

				Logger.Sources.Remove(preloaderLogSource);	
			}

			if (logListener != null)
				Logger.Listeners.Add(logListener);

			if (Utility.CurrentOs == Platform.Linux)
			{
				Logger.LogInfo($"Detected Unity version: v{Application.unityVersion}");
			}

			Logger.LogMessage("Chainloader ready");

			_initialized = true;
		}

		private static Regex allowedGuidRegex { get; } = new Regex(@"^[a-zA-Z0-9\._\-]+$");

		public static PluginInfo ToPluginInfo(TypeDefinition type)
		{
			if (type.IsInterface || type.IsAbstract)
				return null;

			try
			{
				if (!type.IsSubtypeOf(typeof(BaseUnityPlugin)))
					return null;
			}
			catch (AssemblyResolutionException)
			{
				// Can happen if this type inherits a type from an assembly that can't be found. Safe to assume it's not a plugin.
				return null;
			}

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

			var bepinVersion = type.Module.AssemblyReferences.FirstOrDefault(reference => reference.Name == "BepInEx")?.Version ?? new Version();

			return new PluginInfo
			{
				Metadata = metadata,
				Processes = filters,
				Dependencies = dependencies,
				Incompatibilities = incompatibilities,
				TypeName = type.FullName,
				TargettedBepInExVersion = bepinVersion
			};
		}

		private static readonly string CurrentAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;
		private static readonly Version CurrentAssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;

		private static bool HasBepinPlugins(AssemblyDefinition ass)
		{
			if (ass.MainModule.AssemblyReferences.All(r => r.Name != CurrentAssemblyName))
				return false;
			if (ass.MainModule.GetTypeReferences().All(r => r.FullName != typeof(BaseUnityPlugin).FullName))
				return false;

			return true;
		}

		private static bool PluginTargetsWrongBepin(PluginInfo pluginInfo)
		{
			var pluginTarget = pluginInfo.TargettedBepInExVersion;
			// X.X.X.x - compare normally. x.x.x.X - nightly build number, ignore
			if (pluginTarget.Major != CurrentAssemblyVersion.Major) return true;
			if (pluginTarget.Minor > CurrentAssemblyVersion.Minor) return true;
			if (pluginTarget.Minor < CurrentAssemblyVersion.Minor) return false;
			return pluginTarget.Build > CurrentAssemblyVersion.Build;
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

				if (ConsoleManager.ConsoleActive)
					ConsoleManager.SetConsoleTitle($"{CurrentAssemblyName} {CurrentAssemblyVersion} - {productNameProp?.GetValue(null, null) ?? Paths.ProcessName}");

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

				// We use a sorted dictionary to ensure consistent load order
				var dependencyDict = new SortedDictionary<string, IEnumerable<string>>(StringComparer.InvariantCultureIgnoreCase);
				var pluginsByGUID = new Dictionary<string, PluginInfo>();

				foreach (var pluginInfoGroup in pluginInfos.GroupBy(info => info.Metadata.GUID))
				{
					PluginInfo loadedVersion = null;
					foreach (var pluginInfo in pluginInfoGroup.OrderByDescending(x => x.Metadata.Version))
					{
						if (loadedVersion != null)
						{
							Logger.LogWarning($"Skipping [{pluginInfo}] because a newer version exists ({loadedVersion})");
							continue;
						}

						loadedVersion = pluginInfo;

						// Perform checks that will prevent loading plugins in this run
						var filters = pluginInfo.Processes.ToList();
						bool invalidProcessName = filters.Count != 0 && filters.All(x => !string.Equals(x.ProcessName.Replace(".exe", ""), Paths.ProcessName, StringComparison.InvariantCultureIgnoreCase));

						if (invalidProcessName)
						{
							Logger.LogWarning($"Skipping [{pluginInfo}] because of process filters ({string.Join(", ", pluginInfo.Processes.Select(p => p.ProcessName).ToArray())})");
							continue;
						}

						dependencyDict[pluginInfo.Metadata.GUID] = pluginInfo.Dependencies.Select(d => d.DependencyGUID);
						pluginsByGUID[pluginInfo.Metadata.GUID] = pluginInfo;
					}
				}

				foreach (var pluginInfo in pluginsByGUID.Values.ToList())
				{
					if (pluginInfo.Incompatibilities.Any(incompatibility => pluginsByGUID.ContainsKey(incompatibility.IncompatibilityGUID)))
					{
						pluginsByGUID.Remove(pluginInfo.Metadata.GUID);
						dependencyDict.Remove(pluginInfo.Metadata.GUID);

						var incompatiblePlugins = pluginInfo.Incompatibilities.Select(x => x.IncompatibilityGUID).Where(x => pluginsByGUID.ContainsKey(x)).ToArray();
						string message = $@"Could not load [{pluginInfo}] because it is incompatible with: {string.Join(", ", incompatiblePlugins)}";
						DependencyErrors.Add(message);
						Logger.LogError(message);
					}
					else if (PluginTargetsWrongBepin(pluginInfo))
					{
						string message = $@"Plugin [{pluginInfo}] targets a wrong version of BepInEx ({pluginInfo.TargettedBepInExVersion}) and might not work until you update";
						DependencyErrors.Add(message);
						Logger.LogWarning(message);
					}
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

					processedPlugins.Add(pluginGUID, pluginInfo.Metadata.Version);

					if (dependsOnInvalidPlugin)
					{
						string message = $"Skipping [{pluginInfo}] because it has a dependency that was not loaded. See previous errors for details.";
						DependencyErrors.Add(message);
						Logger.LogWarning(message);
						continue;
					}

					if (missingDependencies.Count != 0)
					{
						bool IsEmptyVersion(Version v) => v.Major == 0 && v.Minor == 0 && v.Build <= 0 && v.Revision <= 0;

						string message = $@"Could not load [{pluginInfo}] because it has missing dependencies: {
							string.Join(", ", missingDependencies.Select(s => IsEmptyVersion(s.MinimumVersion) ? s.DependencyGUID : $"{s.DependencyGUID} (v{s.MinimumVersion} or newer)").ToArray())
							}";
						DependencyErrors.Add(message);
						Logger.LogError(message);

						invalidPlugins.Add(pluginGUID);
						continue;
					}

					try
					{
						Logger.LogInfo($"Loading [{pluginInfo}]");

						if (!loadedAssemblies.TryGetValue(pluginInfo.Location, out var ass))
							loadedAssemblies[pluginInfo.Location] = ass = Assembly.LoadFile(pluginInfo.Location);

						PluginInfos[pluginGUID] = pluginInfo;
						pluginInfo.Instance = (BaseUnityPlugin)ManagerObject.AddComponent(ass.GetType(pluginInfo.TypeName));

						_plugins.Add(pluginInfo.Instance);
					}
					catch (Exception ex)
					{
						invalidPlugins.Add(pluginGUID);
						PluginInfos.Remove(pluginGUID);

						Logger.LogError($"Error loading [{pluginInfo}] : {ex.Message}");
						if (ex is ReflectionTypeLoadException re)
							Logger.LogDebug(TypeLoader.TypeLoadExceptionToString(re));
						else
							Logger.LogDebug(ex);
					}
				}
			}
			catch (Exception ex)
			{
				try
				{
					ConsoleManager.CreateConsole();
				}
				catch { }

				Logger.LogFatal("Error occurred starting the game");
				Logger.LogFatal(ex.ToString());
			}

			Logger.LogMessage("Chainloader startup complete");

			_loaded = true;
		}

		#region Config


		private static readonly ConfigEntry<bool> ConfigUnityLogging = ConfigFile.CoreConfig.Bind(
			"Logging", "UnityLogListening",
			true,
			"Enables showing unity log messages in the BepInEx logging system.");

		private static readonly ConfigEntry<bool> ConfigDiskWriteUnityLog = ConfigFile.CoreConfig.Bind(
			"Logging.Disk", "WriteUnityLog",
			false,
			"Include unity log messages in log file output.");

		private static readonly ConfigEntry<bool> ConfigDiskAppend = ConfigFile.CoreConfig.Bind(
			"Logging.Disk", "AppendLog",
			false,
			"Appends to the log file instead of overwriting, on game startup.");

		private static readonly ConfigEntry<bool> ConfigDiskLogging = ConfigFile.CoreConfig.Bind(
			"Logging.Disk", "Enabled",
			true,
			"Enables writing log messages to disk.");

		private static readonly ConfigEntry<LogLevel> ConfigDiskConsoleDisplayedLevel = ConfigFile.CoreConfig.Bind(
			"Logging.Disk", "LogLevels",
			LogLevel.Fatal | LogLevel.Error | LogLevel.Message | LogLevel.Info | LogLevel.Warning,
			"Which log leves are saved to the disk log output.");
		#endregion
	}
}