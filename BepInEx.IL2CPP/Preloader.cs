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

		private static ManualLogSource Log => PreloaderLogger.Log;

		public static IL2CPPChainloader Chainloader { get; private set; }

		public static void Run()
		{
			try
			{

				PreloaderLog = new PreloaderConsoleListener(false);
				Logger.Listeners.Add(PreloaderLog);


				BasicLogInfo.PrintLogInfo(Log);



				Log.LogInfo($"Running under Unity v{FileVersionInfo.GetVersionInfo(Paths.ExecutablePath).FileVersion}");

				Log.LogDebug($"Game executable path: {Paths.ExecutablePath}");
				Log.LogDebug($"Unhollowed assembly directory: {IL2CPPUnhollowedPath}");
				Log.LogDebug($"BepInEx root path: {Paths.BepInExRootPath}");

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