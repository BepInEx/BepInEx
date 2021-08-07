using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.NetLauncher.RuntimeFixes;
using BepInEx.Preloader.Core;
using BepInEx.Preloader.Core.Logging;
using BepInEx.Preloader.Core.Patching;
using MonoMod.Utils;

namespace BepInEx.NetLauncher
{
    public static class NetPreloader
    {
        private static readonly ManualLogSource Log = PreloaderLogger.Log;

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool AddDllDirectory(string lpPathName);


        public static void Start(string[] args)
        {
            var preloaderListener = new PreloaderConsoleListener();
            Logger.Listeners.Add(preloaderListener);

            if (string.IsNullOrEmpty(ConfigEntrypointExecutable.Value))
            {
                Log.LogFatal($"Entry executable was not set. Please set this in your config before launching the application");
                Program.ReadExit();
                return;
            }

            var executablePath = Path.GetFullPath(ConfigEntrypointExecutable.Value);

            if (!File.Exists(executablePath))
            {
                Log.LogFatal($"Unable to locate executable: {ConfigEntrypointExecutable.Value}");
                Program.ReadExit();
                return;
            }


            Paths.SetExecutablePath(executablePath);
            Program.ResolveDirectories.Add(Paths.GameRootPath);

            foreach (var searchDir in Program.ResolveDirectories)
                TypeLoader.SearchDirectories.Add(searchDir);

            if (PlatformHelper.Is(Platform.Windows))
            {
                AddDllDirectory(Paths.GameRootPath);
                SetDllDirectory(Paths.GameRootPath);
            }


            Logger.Sources.Add(TraceLogSource.CreateSource());

            ChainloaderLogHelper.PrintLogInfo(Log);

            Log.LogInfo($"CLR runtime version: {Environment.Version}");

            Log.LogMessage("Preloader started");

            Assembly entrypointAssembly;

            using (var assemblyPatcher = new AssemblyPatcher())
            {
                assemblyPatcher.AddPatchersFromDirectory(Paths.PatcherPluginPath);

                Log.LogInfo($"{assemblyPatcher.PatcherContext.PatchDefinitions.Count} patcher definition(s) loaded");

                assemblyPatcher.LoadAssemblyDirectories(new[] { Paths.GameRootPath }, new[] { "dll", "exe" });

                Log.LogInfo($"{assemblyPatcher.PatcherContext.AvailableAssemblies.Count} assemblies discovered");

                assemblyPatcher.PatchAndLoad();


                var assemblyName = AssemblyName.GetAssemblyName(executablePath);

                entrypointAssembly =
                    assemblyPatcher.PatcherContext.LoadedAssemblies.Values.FirstOrDefault(x => x.FullName == assemblyName.FullName);

                foreach (var loadedAssembly in assemblyPatcher.PatcherContext.LoadedAssemblies)
                {
                    // TODO: Need full paths for loaded assemblies
                    var assemblyPath = Path.Combine(Paths.GameRootPath, loadedAssembly.Key);

                    Log.LogDebug($"Registering '{assemblyPath}' as a loaded assembly");
                    AssemblyFixes.AssemblyLocations[loadedAssembly.Value.FullName] = assemblyPath;
                }

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

            Logger.Listeners.Remove(preloaderListener);

            var chainloader = new NetChainloader();
            chainloader.Initialize();
            chainloader.Execute();


            AssemblyFixes.Execute(entrypointAssembly);

            try
            {
                entrypointAssembly.EntryPoint.Invoke(null, new[] { args });
            }
            catch (Exception ex)
            {
                Log.LogFatal($"Unhandled exception: {ex}");
            }
        }

        #region Config

        private static readonly ConfigEntry<string> ConfigEntrypointExecutable = ConfigFile.CoreConfig.Bind<string>(
         "Preloader.Entrypoint", "Assembly",
         null,
         "The local filename of the .NET executable to target.");

        #endregion
    }
}
