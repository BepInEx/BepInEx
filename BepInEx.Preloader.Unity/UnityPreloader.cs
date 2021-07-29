using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Preloader.Core;
using BepInEx.Preloader.Core.Logging;
using BepInEx.Preloader.Core.Patching;
using BepInEx.Preloader.RuntimeFixes;
using MonoMod.Utils;

namespace BepInEx.Preloader.Unity
{
    /// <summary>
    ///     The main entrypoint of BepInEx, and initializes all patchers and the chainloader.
    /// </summary>
    internal static class UnityPreloader
    {
        /// <summary>
        ///     The log writer that is specific to the preloader.
        /// </summary>
        private static PreloaderConsoleListener PreloaderLog { get; set; }

        private static ManualLogSource Log => PreloaderLogger.Log;

        public static bool IsPostUnity2017 { get; } =
            File.Exists(Path.Combine(Paths.ManagedPath, "UnityEngine.CoreModule.dll"));

        public static void Run()
        {
            try
            {
                InitializeHarmony();

                ConsoleManager.Initialize(false);
                AllocateConsole();

                Utility.TryDo(() =>
                {
                    if (ConfigApplyRuntimePatches.Value)
                        UnityPatches.Apply();
                }, out var runtimePatchException);

                Logger.Sources.Add(new HarmonyLogSource());
                Logger.Sources.Add(TraceLogSource.CreateSource());

                Logger.Listeners.Add(new ConsoleLogListener());
                PreloaderLog = new PreloaderConsoleListener();
                Logger.Listeners.Add(PreloaderLog);

                ChainloaderLogHelper.PrintLogInfo(Log);

                Log.LogInfo($"Running under Unity v{GetUnityVersion()}");
                Log.LogInfo($"CLR runtime version: {Environment.Version}");
                Log.LogInfo($"Supports SRE: {Utility.CLRSupportsDynamicAssemblies}");

                Log.LogDebug($"Game executable path: {Paths.ExecutablePath}");
                Log.LogDebug($"Unity Managed directory: {Paths.ManagedPath}");
                Log.LogDebug($"BepInEx root path: {Paths.BepInExRootPath}");

                if (runtimePatchException != null)
                    Log.LogWarning($"Failed to apply runtime patches for Mono. See more info in the output log. Error message: {runtimePatchException.Message}");

                Log.LogMessage("Preloader started");

                TypeLoader.SearchDirectories.UnionWith(Paths.DllSearchPaths);

                using (var assemblyPatcher = new AssemblyPatcher())
                {
                    assemblyPatcher.AddPatchersFromDirectory(Paths.BepInExAssemblyDirectory);
                    assemblyPatcher.AddPatchersFromDirectory(Paths.PatcherPluginPath);

                    Log.LogInfo($"{assemblyPatcher.PatcherContext.PatcherPlugins.Count} patcher plugin{(assemblyPatcher.PatcherContext.PatcherPlugins.Count == 1 ? "" : "s")} loaded");

                    assemblyPatcher.LoadAssemblyDirectories(Paths.DllSearchPaths);

                    Log.LogInfo($"{assemblyPatcher.PatcherContext.AvailableAssemblies.Count} assemblies discovered");

                    assemblyPatcher.PatchAndLoad();
                }


                Log.LogMessage("Preloader finished");

                Logger.Listeners.Remove(PreloaderLog);

                PreloaderLog.Dispose();
            }
            catch (Exception ex)
            {
                try
                {
                    Log.LogFatal("Could not run preloader!");
                    Log.LogFatal(ex);

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
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to allocate console!");
                Log.LogError(ex);
            }
        }

        public static string GetUnityVersion()
        {
            if (PlatformHelper.Is(Platform.Windows))
                return FileVersionInfo.GetVersionInfo(Paths.ExecutablePath).FileVersion;

            return $"Unknown ({(IsPostUnity2017 ? "post" : "pre")}-2017)";
        }

        private static void InitializeHarmony()
        {
            switch (ConfigHarmonyBackend.Value)
            {
                case MonoModBackend.auto:
                    break;
                case MonoModBackend.dynamicmethod:
                case MonoModBackend.methodbuilder:
                case MonoModBackend.cecil:
                    Environment.SetEnvironmentVariable("MONOMOD_DMD_TYPE", ConfigHarmonyBackend.Value.ToString());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(ConfigHarmonyBackend), ConfigHarmonyBackend.Value,
                                                          "Unknown backend");
            }
        }

        private enum MonoModBackend
        {
            // Enum names are important!
            [Description("Auto")]
            auto = 0,

            [Description("DynamicMethod")]
            dynamicmethod,

            [Description("MethodBuilder")]
            methodbuilder,

            [Description("Cecil")]
            cecil
        }

        #region Config

        internal static readonly ConfigEntry<bool> ConfigApplyRuntimePatches = ConfigFile.CoreConfig.Bind(
         "Preloader", "ApplyRuntimePatches",
         true,
         "Enables or disables runtime patches.\nThis should always be true, unless you cannot start the game due to a Harmony related issue (such as running .NET Standard runtime) or you know what you're doing.");

        private static readonly ConfigEntry<MonoModBackend> ConfigHarmonyBackend = ConfigFile.CoreConfig.Bind(
         "Preloader",
         "HarmonyBackend",
         MonoModBackend.auto,
         "Specifies which MonoMod backend to use for Harmony patches. Auto uses the best available backend.\nThis setting should only be used for development purposes (e.g. debugging in dnSpy). Other code might override this setting.");

        #endregion
    }
}
