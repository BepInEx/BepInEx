using System;
using System.Diagnostics;
using System.IO;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using BepInEx.NetLauncher.Common;
using BepInEx.Preloader.Core;
using BepInEx.Preloader.Core.Logging;
using BepInEx.Preloader.Core.Patching;
using Mono.Cecil;

namespace BepInEx.NetCore
{
    internal class NetCorePreloader
    {
        private static readonly ManualLogSource Log = PreloaderLogger.Log;

        public static void Start()
        {
            var preloaderListener = new PreloaderConsoleListener();
            Logger.Listeners.Add(preloaderListener);

            var entrypointAssemblyPath = Path.ChangeExtension(Process.GetCurrentProcess().MainModule.FileName, "dll");

            Paths.SetExecutablePath(entrypointAssemblyPath);

            TypeLoader.SearchDirectories.Add(Path.GetDirectoryName(entrypointAssemblyPath));
            
            Logger.Sources.Add(TraceLogSource.CreateSource());

            ChainloaderLogHelper.PrintLogInfo(Log);

            Log.Log(LogLevel.Info, $"CLR runtime version: {Environment.Version}");


            AssemblyBuildInfo executableInfo;

            using (var entrypointAssembly = AssemblyDefinition.ReadAssembly(entrypointAssemblyPath))
                executableInfo = AssemblyBuildInfo.DetermineInfo(entrypointAssembly);
            
            Log.LogInfo($"Game executable build architecture: {executableInfo}");

            Log.Log(LogLevel.Message, "Preloader started");

            using (var assemblyPatcher = new AssemblyPatcher())
            {
                assemblyPatcher.AddPatchersFromDirectory(Paths.PatcherPluginPath);

                Log.Log(LogLevel.Info,
                        $"{assemblyPatcher.PatcherContext.PatchDefinitions.Count} patcher definition(s) loaded");

                assemblyPatcher.LoadAssemblyDirectories(new[] { Paths.GameRootPath }, new[] { "dll", "exe" });

                Log.Log(LogLevel.Info, $"{assemblyPatcher.PatcherContext.AvailableAssemblies.Count} assemblies discovered");

                assemblyPatcher.PatchAndLoad();
            }

            Log.LogMessage("Preloader finished");

            Logger.Listeners.Remove(preloaderListener);

            var chainloader = new NetChainloader();
            chainloader.Initialize();
            chainloader.Execute();
        }
    }
}
