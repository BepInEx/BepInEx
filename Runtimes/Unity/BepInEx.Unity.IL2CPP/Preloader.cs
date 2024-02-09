using System;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.IL2CPP.RuntimeFixes;
using BepInEx.Logging;
using BepInEx.Preloader.Core;
using BepInEx.Preloader.Core.Logging;
using BepInEx.Preloader.Core.Patching;
using BepInEx.Preloader.RuntimeFixes;
using BepInEx.Unity.Common;
using MonoMod.Utils;

namespace BepInEx.Unity.IL2CPP;

public static class Preloader
{
    private static PreloaderConsoleListener PreloaderLog { get; set; }

    internal static ManualLogSource Log => PreloaderLogger.Log;

    // TODO: This is not needed, maybe remove? (Instance is saved in IL2CPPChainloader itself)
    private static IL2CPPChainloader Chainloader { get; set; }

    /// <summary>
    ///     Path to a folder containing all of the IL2CPP interop assemblies for the current session.
    ///     Interop assemblies are used to provide a bridge between managed plugins and C++ compiled game code.
    ///     They are loaded automatically when referenced by managed code but not when passing objects between managed and unmanaged code.
    /// </summary>
    public static string GetInteropAssemblyPath() => Il2CppInteropManager.IL2CPPInteropAssemblyPath;

    public static void Run()
    {
        try
        {
            HarmonyBackendFix.Initialize();
            ConsoleSetOutFix.Apply();
            UnityInfo.Initialize(Paths.ExecutablePath, Paths.GameDataPath);

            ConsoleManager.Initialize(false, true);

            PreloaderLog = new PreloaderConsoleListener();
            Logger.Listeners.Add(PreloaderLog);

            if (ConsoleManager.ConsoleEnabled)
            {
                ConsoleManager.CreateConsole();
                Logger.Listeners.Add(new ConsoleLogListener());
            }

            RedirectStdErrFix.Apply();

            ChainloaderLogHelper.PrintLogInfo(Log);

            Logger.Log(LogLevel.Info, $"Running under Unity {UnityInfo.Version}");
            Logger.Log(LogLevel.Info, $"Runtime version: {Environment.Version}");
            Logger.Log(LogLevel.Info, $"Runtime information: {RuntimeInformation.FrameworkDescription}");

            Logger.Log(LogLevel.Debug, $"Game executable path: {Paths.ExecutablePath}");
            Logger.Log(LogLevel.Debug, $"Interop assembly directory: {Il2CppInteropManager.IL2CPPInteropAssemblyPath}");
            Logger.Log(LogLevel.Debug, $"BepInEx root path: {Paths.BepInExRootPath}");

            if (PlatformHelper.Is(Platform.Wine) && !Environment.Is64BitProcess)
            {
                if (!NativeLibrary.TryGetExport(NativeLibrary.Load("ntdll"), "RtlRestoreContext", out var _))
                {
                    Logger.Log(LogLevel.Warning,
                               "Your wine version doesn't support CoreCLR properly, expect crashes! Upgrade to wine 7.16 or higher.");
                }
            }

            NativeLibrary.SetDllImportResolver(typeof(Il2CppInterop.Runtime.IL2CPP).Assembly, DllImportResolver);

            Il2CppInteropManager.Initialize();

            using (var assemblyPatcher = new AssemblyPatcher((data, _) => Assembly.Load(data)))
            {
                assemblyPatcher.AddPatchersFromDirectory(Paths.PatcherPluginPath);

                Log.LogInfo($"{assemblyPatcher.PatcherContext.PatcherPlugins.Count} patcher plugin{(assemblyPatcher.PatcherContext.PatcherPlugins.Count == 1 ? "" : "s")} loaded");

                assemblyPatcher.LoadAssemblyDirectories(Il2CppInteropManager.IL2CPPInteropAssemblyPath);

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

    private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName == "GameAssembly")
        {
            return NativeLibrary.Load(Il2CppInteropManager.GameAssemblyPath, assembly, searchPath);
        }

        return IntPtr.Zero;
    }
}
