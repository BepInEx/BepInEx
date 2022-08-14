using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using BepInEx.NET.Common;
using BepInEx.Preloader.Core;
using BepInEx.Preloader.Core.Logging;
using BepInEx.Preloader.Core.Patching;
using Mono.Cecil;

namespace BepInEx.NET.CoreCLR
{
    internal class NetCorePreloader
    {
        private static readonly ManualLogSource Log = PreloaderLogger.Log;

        public static void Start()
        {
            var preloaderListener = new PreloaderConsoleListener();
            Logger.Listeners.Add(preloaderListener);

            string entrypointAssemblyPath = !Paths.ExecutablePath.EndsWith(StartupHook.DoesNotExistPath) ? Paths.ExecutablePath : null;

            TypeLoader.SearchDirectories.Add(Paths.GameRootPath);
            
            Logger.Sources.Add(TraceLogSource.CreateSource());

            ChainloaderLogHelper.PrintLogInfo(Log);

            Log.LogInfo($"CLR runtime version: {Environment.Version}");

            Log.LogInfo($"Current executable: {Process.GetCurrentProcess().MainModule.FileName}");
            Log.LogInfo($"Entrypoint assembly: {entrypointAssemblyPath ?? "<does not exist>"}");
            Log.LogInfo($"Launch arguments: {string.Join(' ', Environment.GetCommandLineArgs())}");


            AssemblyBuildInfo executableInfo;

            if (entrypointAssemblyPath != null)
            {
                using (var entrypointAssembly = AssemblyDefinition.ReadAssembly(entrypointAssemblyPath))
                    executableInfo = AssemblyBuildInfo.DetermineInfo(entrypointAssembly);

                Log.LogInfo($"Game executable build architecture: {executableInfo}");
            }
            else
            {
                Log.LogWarning("Game assembly is unknown, can't determine build architecture");
            }

            Log.LogMessage("Preloader started");

            using (var assemblyPatcher = new AssemblyPatcher((data, _) => Assembly.Load(data)))
            {
                assemblyPatcher.AddPatchersFromDirectory(Paths.PatcherPluginPath);

                Log.LogInfo($"{assemblyPatcher.PatcherContext.PatchDefinitions.Count} patcher definition(s) loaded");

                assemblyPatcher.LoadAssemblyDirectories(new[] { Paths.GameRootPath }, new[] { "dll", "exe" });

                Log.LogInfo($"{assemblyPatcher.PatcherContext.AvailableAssemblies.Count} assemblies discovered");

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
