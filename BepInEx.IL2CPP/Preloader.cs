using System;
using System.Diagnostics;
using BepInEx.Logging;
using BepInEx.Preloader.Core;
using BepInEx.Preloader.Core.Logging;

namespace BepInEx.IL2CPP
{
	public static class Preloader
	{
		public static string IL2CPPUnhollowedPath { get; internal set; }

		private static PreloaderConsoleListener PreloaderLog { get; set; }

		internal static ManualLogSource Log => PreloaderLogger.Log;
		internal static ManualLogSource UnhollowerLog { get; set; }

		public static IL2CPPChainloader Chainloader { get; private set; }

		public static void Run()
		{
			try
			{
				ConsoleManager.Initialize(false);

				PreloaderLog = new PreloaderConsoleListener();
				Logger.Listeners.Add(PreloaderLog);



				if (ConsoleManager.ConfigConsoleEnabled.Value)
				{
					ConsoleManager.CreateConsole();
					Logger.Listeners.Add(new ConsoleLogListener());
				}

				ChainloaderLogHelper.PrintLogInfo(Log);

				Log.LogInfo($"Running under Unity v{FileVersionInfo.GetVersionInfo(Paths.ExecutablePath).FileVersion}");

				Log.LogDebug($"Game executable path: {Paths.ExecutablePath}");
				Log.LogDebug($"Unhollowed assembly directory: {IL2CPPUnhollowedPath}");
				Log.LogDebug($"BepInEx root path: {Paths.BepInExRootPath}");



				UnhollowerLog = Logger.CreateLogSource("Unhollower");
				UnhollowerBaseLib.LogSupport.InfoHandler += UnhollowerLog.LogInfo;
				UnhollowerBaseLib.LogSupport.WarningHandler += UnhollowerLog.LogWarning;
				UnhollowerBaseLib.LogSupport.TraceHandler += UnhollowerLog.LogDebug;
				UnhollowerBaseLib.LogSupport.ErrorHandler += UnhollowerLog.LogError;


				if (ProxyAssemblyGenerator.CheckIfGenerationRequired())
					ProxyAssemblyGenerator.GenerateAssemblies();


				using (var assemblyPatcher = new AssemblyPatcher())
				{
					assemblyPatcher.AddPatchersFromDirectory(Paths.PatcherPluginPath);

					Log.LogInfo($"{assemblyPatcher.PatcherPlugins.Count} patcher plugin{(assemblyPatcher.PatcherPlugins.Count == 1 ? "" : "s")} loaded");

					assemblyPatcher.LoadAssemblyDirectory(IL2CPPUnhollowedPath);

					Log.LogInfo($"{assemblyPatcher.PatcherPlugins.Count} assemblies discovered");
					
					assemblyPatcher.PatchAndLoad();
				}


				Logger.Listeners.Remove(PreloaderLog);



				Chainloader = new IL2CPPChainloader();

				Chainloader.Initialize();
			}
			catch (Exception ex)
			{
				Log.LogFatal(ex);

				throw;
			}
		}
	}
}