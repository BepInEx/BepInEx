using System;
using System.Diagnostics;
using System.Globalization;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Preloader.Core;
using BepInEx.Preloader.Core.Logging;
using BepInEx.Preloader.Core.Patching;
using BepInEx.Preloader.RuntimeFixes;
using UnhollowerBaseLib;
using UnhollowerBaseLib.Runtime;

namespace BepInEx.IL2CPP;

public static class Preloader
{
    #region Config

    private static readonly ConfigEntry<string> ConfigUnityVersion = ConfigFile.CoreConfig.Bind(
     "IL2CPP", "UnityVersion",
     string.Empty,
     "Unity version to report to Il2CppUnhollower. If empty, version is automatically determined from the game process.");

    #endregion

    public static string IL2CPPUnhollowedPath { get; internal set; }

    private static PreloaderConsoleListener PreloaderLog { get; set; }

    internal static ManualLogSource Log => PreloaderLogger.Log;
    internal static ManualLogSource UnhollowerLog { get; set; }

    // TODO: This is not needed, maybe remove? (Instance is saved in IL2CPPChainloader itself)
    private static IL2CPPChainloader Chainloader { get; set; }

    public static Version UnityVersion { get; private set; }

    public static void Run()
    {
        try
        {
            HarmonyBackendFix.Initialize();

            ConsoleManager.Initialize(false, false);

            PreloaderLog = new PreloaderConsoleListener();
            Logger.Listeners.Add(PreloaderLog);

            if (ConsoleManager.ConsoleEnabled)
            {
                ConsoleManager.CreateConsole();
                Logger.Listeners.Add(new ConsoleLogListener());
            }

            ChainloaderLogHelper.PrintLogInfo(Log);

            Log.Log(LogLevel.Debug, $"Game executable path: {Paths.ExecutablePath}");
            Log.Log(LogLevel.Debug, $"Unhollowed assembly directory: {IL2CPPUnhollowedPath}");
            Log.Log(LogLevel.Debug, $"BepInEx root path: {Paths.BepInExRootPath}");

            UnhollowerLog = Logger.CreateLogSource("Unhollower");
            LogSupport.InfoHandler += UnhollowerLog.LogInfo;
            LogSupport.WarningHandler += UnhollowerLog.LogWarning;
            LogSupport.TraceHandler += UnhollowerLog.LogDebug;
            LogSupport.ErrorHandler += UnhollowerLog.LogError;

            InitializeUnityVersion();

            if (ProxyAssemblyGenerator.CheckIfGenerationRequired())
                ProxyAssemblyGenerator.GenerateAssemblies();

            UnityVersionHandler.Initialize(UnityVersion.Major, UnityVersion.Minor, UnityVersion.Build);

            using (var assemblyPatcher = new AssemblyPatcher())
            {
                assemblyPatcher.AddPatchersFromDirectory(Paths.PatcherPluginPath);

                Log.LogInfo($"{assemblyPatcher.PatcherContext.PatcherPlugins.Count} patcher plugin{(assemblyPatcher.PatcherContext.PatcherPlugins.Count == 1 ? "" : "s")} loaded");

                assemblyPatcher.LoadAssemblyDirectories(IL2CPPUnhollowedPath);

                Log.LogInfo($"{assemblyPatcher.PatcherContext.PatcherPlugins.Count} assemblies discovered");

                assemblyPatcher.PatchAndLoad();
            }


            Logger.Listeners.Remove(PreloaderLog);


            Chainloader = new IL2CPPChainloader();

            Chainloader.Initialize();
        }
        catch (Exception ex)
        {
            Log.Log(LogLevel.Fatal, ex);

            throw;
        }
    }

    private static void InitializeUnityVersion()
    {
        if (TryInitializeUnityVersion(ConfigUnityVersion.Value))
            Log.Log(LogLevel.Warning, "Unity version obtained from the config.");
        else if (TryInitializeUnityVersion(Process.GetCurrentProcess().MainModule.FileVersionInfo.FileVersion))
            Log.Log(LogLevel.Debug, "Unity version obtained from main application module.");
        else
            Log.Log(LogLevel.Error, "Running under default Unity version. UnityVersionHandler is not initialized.");
    }

    private static bool TryInitializeUnityVersion(string version)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(version))
                return false;

            var parts = version.Split('.');
            var major = 0;
            var minor = 0;
            var build = 0;

            // Issue #229 - Don't use Version.Parse("2019.4.16.14703470L&ProductVersion")
            var success = int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out major);
            if (success && parts.Length > 1)
                success = int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minor);
            if (success && parts.Length > 2)
                success = int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out build);

            if (!success)
            {
                Log.LogError($"Failed to parse Unity version: {version}");
                return false;
            }

            UnityVersion = new Version(major, minor, build);
            Log.LogInfo($"Running under Unity v{UnityVersion}");
            return true;
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to parse Unity version: {ex}");
            return false;
        }
    }
}
