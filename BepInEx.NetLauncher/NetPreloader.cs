using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.NetLauncher.RuntimeFixes;
using BepInEx.Preloader.Core;
using BepInEx.Preloader.Core.Logging;

namespace BepInEx.NetLauncher
{
	public static class NetPreloader
	{
		private static readonly ManualLogSource Log = PreloaderLogger.Log;


		public static void Start(string[] args)
		{
			if (string.IsNullOrEmpty(ConfigEntrypointExecutable.Value))
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

			Logger.Sources.Add(TraceLogSource.CreateSource());

			ChainloaderLogHelper.PrintLogInfo(Log);

			Log.LogInfo($"CLR runtime version: {Environment.Version}");

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

		#endregion
	}
}
