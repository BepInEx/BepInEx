using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.NetLauncher.RuntimeFixes;
using BepInEx.Preloader.Core;
using MonoMod.RuntimeDetour;

namespace BepInEx.NetLauncher
{
	public static class NetPreloader
	{
		private static readonly ManualLogSource Log = PreloaderLogger.Log;


		public static void Start(string[] args)
		{
			if (ConfigEntrypointExecutable.Value == null)
			{
				Log.LogFatal($"Entry executable was not set. Please set this in your config before launching the application");
				Program.ReadExit();
				return;
			}

			string executablePath = Path.GetFullPath(ConfigEntrypointExecutable.Value);

			if (!File.Exists(executablePath))
			{
				Log.LogFatal($"Unable to locate executable: {ConfigEntrypointExecutable.Value}");
				Program.ReadExit();
				return;
			}

			Paths.SetExecutablePath(executablePath);
			Program.ResolveDirectories.Add(Paths.GameRootPath);
			TypeLoader.SearchDirectories.Add(Paths.GameRootPath);

			bool bridgeInitialized = Utility.TryDo(() =>
			{
				if (ConfigShimHarmony.Value)
					HarmonyDetourBridge.Init();
			}, out var harmonyBridgeException);

			Logger.Sources.Add(TraceLogSource.CreateSource());

			string consoleTile = $"BepInEx {typeof(Paths).Assembly.GetName().Version} - {Process.GetCurrentProcess().ProcessName}";
			Log.LogMessage(consoleTile);

			if (ConsoleManager.ConsoleActive)
				ConsoleManager.SetConsoleTitle(consoleTile);

			//See BuildInfoAttribute for more information about this section.
			object[] attributes = typeof(BuildInfoAttribute).Assembly.GetCustomAttributes(typeof(BuildInfoAttribute), false);

			if (attributes.Length > 0)
			{
				var attribute = (BuildInfoAttribute)attributes[0];
				Log.LogMessage(attribute.Info);
			}

			Log.LogInfo($"CLR runtime version: {Environment.Version}");

			if (harmonyBridgeException != null)
				Log.LogWarning($"Failed to enable fix for Harmony for .NET Standard API. Error message: {harmonyBridgeException.Message}");

			Log.LogMessage("Preloader started");

			Assembly entrypointAssembly;

			using (var assemblyPatcher = new AssemblyPatcher())
			{
				assemblyPatcher.AddPatchersFromDirectory(Paths.PatcherPluginPath);

				Log.LogInfo($"{assemblyPatcher.PatcherPlugins.Count} patcher plugin(s) loaded");

				assemblyPatcher.LoadAssemblyDirectory(Paths.GameRootPath, "dll", "exe");

				Log.LogInfo($"{assemblyPatcher.AssembliesToPatch.Count} assemblies discovered");

				assemblyPatcher.PatchAndLoad();


				var assemblyName = AssemblyName.GetAssemblyName(executablePath);

				entrypointAssembly = assemblyPatcher.LoadedAssemblies.Values.FirstOrDefault(x => x.FullName == assemblyName.FullName);

				if (entrypointAssembly != null)
				{
					Log.LogDebug("Found patched entrypoint assembly! Using it");
				}
				else
				{
					Log.LogDebug("Using entrypoint assembly from disk");
					entrypointAssembly = Assembly.LoadFrom(executablePath);
				}
			}

			Log.LogMessage("Preloader finished");

			var chainloader = new NetChainloader();
			chainloader.Initialize();
			chainloader.Execute();


			AssemblyFix.Execute(entrypointAssembly);

			entrypointAssembly.EntryPoint.Invoke(null, new [] { args });
		}

		#region Config

		private static readonly ConfigEntry<string> ConfigEntrypointExecutable = ConfigFile.CoreConfig.Bind<string>(
			"Preloader.Entrypoint", "Assembly",
			null,
			"The local filename of the .NET executable to target.");

		private static readonly ConfigEntry<bool> ConfigShimHarmony = ConfigFile.CoreConfig.Bind(
			"Preloader", "ShimHarmonySupport",
			!Utility.CLRSupportsDynamicAssemblies,
			"If enabled, basic Harmony functionality is patched to use MonoMod's RuntimeDetour instead.\nTry using this if Harmony does not work in a game.");

		private static readonly ConfigEntry<bool> ConfigPreloaderCOutLogging = ConfigFile.CoreConfig.Bind(
			"Logging", "PreloaderConsoleOutRedirection",
			true,
			"Redirects text from Console.Out during preloader patch loading to the BepInEx logging system.");

		#endregion
	}
}
