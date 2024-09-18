using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Preloader.Core;
using BepInEx.Preloader.Core.Logging;
using BepInEx.Preloader.Core.Patching;
using BepInEx.Preloader.RuntimeFixes;
using BepInEx.Unity.Common;
using BepInEx.Unity.Mono.Preloader.RuntimeFixes;
using BepInEx.Unity.Mono.Preloader.Utils;
using HarmonyLib;

namespace BepInEx.Unity.Mono.Preloader;

/// <summary>
///     The main entrypoint of BepInEx, and initializes all patchers and the chainloader.
/// </summary>
internal static class UnityPreloader
{
    #region Config

    internal static readonly ConfigEntry<bool> ConfigApplyRuntimePatches = ConfigFile.CoreConfig.Bind(
     "Preloader", "ApplyRuntimePatches",
     true,
     "Enables or disables runtime patches.\nThis should always be true, unless you cannot start the game due to a Harmony related issue (such as running .NET Standard runtime) or you know what you're doing.");

    #endregion

    /// <summary>
    ///     The log writer that is specific to the preloader.
    /// </summary>
    private static PreloaderConsoleListener PreloaderLog { get; set; }

    private static ManualLogSource Log => PreloaderLogger.Log;

    private static readonly Harmony Harmony = new("BepInEx.Unity.Mono.Preloader");

    public static void Run()
    {
        try
        {
            HarmonyBackendFix.Initialize();
            UnityInfo.Initialize(Paths.ExecutablePath, Paths.GameDataPath);

            ConsoleManager.Initialize(false, false);
            AllocateConsole();

            Utility.TryDo(() =>
            {
                if (ConfigApplyRuntimePatches.Value)
                    UnityPatches.Apply();
            }, out var runtimePatchException);

            Logger.Sources.Add(new HarmonyLogSource());
            Logger.Sources.Add(TraceLogSource.CreateSource());

            PreloaderLog = new PreloaderConsoleListener();
            Logger.Listeners.Add(PreloaderLog);

            ChainloaderLogHelper.PrintLogInfo(Log);
            Log.Log(LogLevel.Info, $"Running under Unity {UnityInfo.Version}");
            Log.Log(LogLevel.Info, $"CLR runtime version: {Environment.Version}");
            Log.Log(LogLevel.Info, $"Supports SRE: {Utility.CLRSupportsDynamicAssemblies}");

            Log.Log(LogLevel.Debug, $"Game executable path: {Paths.ExecutablePath}");
            Log.Log(LogLevel.Debug, $"Unity Managed directory: {Paths.ManagedPath}");
            Log.Log(LogLevel.Debug, $"BepInEx root path: {Paths.BepInExRootPath}");

            if (runtimePatchException != null)
                Log.Log(LogLevel.Warning,
                        $"Failed to apply runtime patches for Mono. See more info in the output log. Error message: {runtimePatchException.Message}");

            Log.Log(LogLevel.Message, "Preloader started");

            // Set up the chainloader entrypoint which harmony patches the main Unity assembly as soon as possible and
            // unpatch it in our hooking method before calling the chainloader init method
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;

            TypeLoader.SearchDirectories.UnionWith(Paths.DllSearchPaths);

            using (var assemblyPatcher = new AssemblyPatcher(MonoAssemblyHelper.LoadFromMemory))
            {
                assemblyPatcher.AddPatchersFromProviders();

                Log.Log(LogLevel.Info,
                        $"{assemblyPatcher.PatcherContext.PatcherPlugins.Count} patcher plugin{(assemblyPatcher.PatcherContext.PatcherPlugins.Count == 1 ? "" : "s")} loaded");

                assemblyPatcher.LoadAssemblyDirectories(Paths.DllSearchPaths);

                Log.Log(LogLevel.Info,
                        $"{assemblyPatcher.PatcherContext.AvailableAssemblies.Count} assemblies discovered");

                assemblyPatcher.PatchAndLoad();
            }

            Log.Log(LogLevel.Message, "Preloader finished");
            Logger.Listeners.Remove(PreloaderLog);
            PreloaderLog.Dispose();
        }
        catch (Exception ex)
        {
            try
            {
                Log.Log(LogLevel.Fatal, "Could not run preloader!");
                Log.Log(LogLevel.Fatal, ex);

                if (!ConsoleManager.ConsoleActive)
                {
                    //if we've already attached the console, then the log will already be written to the console
                    AllocateConsole();
                    Console.Write(PreloaderLog);
                }
            }
            catch { }

            var log = string.Empty;

            try
            {
                // We could use platform-dependent newlines, however the developers use Windows so this will be easier to read :)

                log = string.Join("\r\n", PreloaderConsoleListener.LogEvents.Select(x => x.ToString()).ToArray());
                log += "\r\n";

                PreloaderLog?.Dispose();
                PreloaderLog = null;
            }
            catch { }

            File.WriteAllText(
                              Path.Combine(Paths.GameRootPath, $"preloader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log"),
                              log + ex);
        }
    }

    // First step of the chainloader entrypoint: Harmony patch the main Unity assembly with a suitable hook
    private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
    {
        const string sceneManagerTypeName = "UnityEngine.SceneManagement.SceneManager";
        const string displayTypeName = "UnityEngine.Display";
        
        var assembly = args.LoadedAssembly;
        var assemblyName = assembly.GetName().Name;

        if (assemblyName is not ("UnityEngine.CoreModule" or "UnityEngine"))
            return;

        AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;

        var sceneManagerType = assembly.GetType(sceneManagerTypeName, false);
        if (sceneManagerType != null)
        {
            var activeSceneChangedMethod = sceneManagerType.GetMethod("Internal_ActiveSceneChanged",
                                                                      BindingFlags.NonPublic | BindingFlags.Static);
            Harmony.Patch(activeSceneChangedMethod, prefix: new HarmonyMethod(typeof(UnityPreloader), nameof(Entrypoint)));
            Log.LogInfo($"Hooked into {activeSceneChangedMethod.FullDescription()}");
            return;
        }

        var displayType = assembly.GetType(displayTypeName, false);
        if (displayType != null)
        {
            var recreateDisplayListMethod = displayType.GetMethod("RecreateDisplayList", BindingFlags.NonPublic | BindingFlags.Static);
            Harmony.Patch(recreateDisplayListMethod, postfix: new HarmonyMethod(typeof(UnityPreloader), nameof(Entrypoint)));
            Log.LogInfo($"Hooked into {recreateDisplayListMethod.FullDescription()}");
            return;
        }

        Log.LogError($"Couldn't find a suitable chainloader entrypoint in the {assemblyName} assembly because " +
                     $"{sceneManagerTypeName} or {displayTypeName} do not exist in the assembly");
    }

    // Second step of the chainloader entrypoint: undo the Harmony patch and call the chainloader init method
    private static void Entrypoint()
    {
        try
        {
            Harmony.UnpatchSelf();

            Assembly.Load("BepInEx.Unity.Mono")
                    .GetType("BepInEx.Unity.Mono.Bootstrap.UnityChainloader")
                    .GetMethod("StaticStart", AccessTools.all)!
                    .Invoke(null, [null]);
        }
        catch (Exception e)
        {
            Log.LogError(e);
        }
    }

    /// <summary>
    ///     Allocates a console window for use by BepInEx safely.
    /// </summary>
    public static void AllocateConsole()
    {
        if (!ConsoleManager.ConsoleEnabled)
            return;

        try
        {
            ConsoleManager.CreateConsole();
            Logger.Listeners.Add(new ConsoleLogListener());
        }
        catch (Exception ex)
        {
            Log.LogError("Failed to allocate console!");
            Log.LogError(ex);
        }
    }
}
