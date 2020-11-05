using BepInEx.Configuration;
using BepInEx.Logging;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Preloader.Core.Logging;
using BepInEx.Unity.Logging;
using MonoMod.Utils;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace BepInEx.Unity.Bootstrap
{
	/// <summary>
	/// The manager and loader for all plugins, and the entry point for BepInEx plugin system.
	/// </summary>
	public class UnityChainloader : BaseChainloader<BaseUnityPlugin>
	{
		/// <summary>
		/// The GameObject that all plugins are attached to as components.
		/// </summary>
		public static GameObject ManagerObject { get; private set; }

		private static void StaticStart(string gameExePath = null)
		{
			var instance = new UnityChainloader();
			instance.Initialize(gameExePath);
			instance.Execute();
		}

		private string _consoleTitle;
		protected override string ConsoleTitle => _consoleTitle;

		public override void Initialize(string gameExePath = null)
		{
			UnityTomlTypeConverters.AddUnityEngineConverters();

			ThreadingHelper.Initialize();

			ManagerObject = new GameObject("BepInEx_Manager");
			UnityEngine.Object.DontDestroyOnLoad(ManagerObject);

			var productNameProp = typeof(Application).GetProperty("productName", BindingFlags.Public | BindingFlags.Static);
			_consoleTitle = $"{CurrentAssemblyName} {CurrentAssemblyVersion} - {productNameProp?.GetValue(null, null) ?? Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().ProcessName)}";

			base.Initialize(gameExePath);
		}

		protected override void InitializeLoggers()
		{
			Logger.Listeners.Add(new UnityLogListener());

			base.InitializeLoggers();


			if (Utility.CurrentPlatform != Platform.Windows)
			{
				Logger.LogInfo($"Detected Unity version: v{Application.unityVersion}");
			}


			if (!ConfigDiskWriteUnityLog.Value)
			{
				DiskLogListener.BlacklistedSources.Add("Unity Log");
			}


			ChainloaderLogHelper.RewritePreloaderLogs();


			if (ConfigUnityLogging.Value)
				Logger.Sources.Add(new UnityLogSource());
		}

		public override BaseUnityPlugin LoadPlugin(PluginInfo pluginInfo, Assembly pluginAssembly)
		{
			return (BaseUnityPlugin)ManagerObject.AddComponent(pluginAssembly.GetType(pluginInfo.TypeName));
		}

		private static readonly ConfigEntry<bool> ConfigUnityLogging = ConfigFile.CoreConfig.Bind(
			"Logging", "UnityLogListening",
			true,
			"Enables showing unity log messages in the BepInEx logging system.");

		private static readonly ConfigEntry<bool> ConfigDiskWriteUnityLog = ConfigFile.CoreConfig.Bind(
			"Logging.Disk", "WriteUnityLog",
			false,
			"Include unity log messages in log file output.");
	}
}