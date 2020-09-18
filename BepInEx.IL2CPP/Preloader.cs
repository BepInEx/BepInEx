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
				PreloaderLog = new PreloaderConsoleListener(true);
				Logger.Listeners.Add(PreloaderLog);



				if (ConsoleManager.ConfigConsoleEnabled.Value && !ConsoleManager.ConsoleActive)
				{
					ConsoleManager.CreateConsole();
					Logger.Listeners.Add(new ConsoleLogListener());
				}

				BasicLogInfo.PrintLogInfo(Log);

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