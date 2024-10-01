using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Core.Bootstrap;
using BepInEx.Logging;
using BepInEx.Preloader.Core.Logging;
using BepInEx.Unity.Common;
using BepInEx.Unity.Mono.Logging;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;
using Object = UnityEngine.Object;

[assembly: InternalsVisibleTo("UnityEngine")]
[assembly: InternalsVisibleTo("UnityEngine.Core")]

namespace BepInEx.Unity.Mono.Bootstrap;

/// <summary>
///     The manager and loader for all plugins, and the entry point for BepInEx plugin system.
/// </summary>
internal class UnityChainloader : Chainloader
{
    private static bool staticStartHasBeenCalled = false;

    private string _consoleTitle;

    /// <summary>
    ///     The GameObject that all plugins are attached to as components.
    /// </summary>
    internal static GameObject ManagerObject { get; private set; }
    
    // In some rare cases calling Application.unityVersion seems to cause MissingMethodException
    // if a preloader patch applies Harmony patch to Chainloader.Initialize.
    // The issue could be related to BepInEx being compiled against Unity 5.6 version of UnityEngine.dll,
    // but the issue is apparently present with both official Harmony and HarmonyX
    // We specifically prevent inlining to prevent early resolving
    // TODO: Figure out better version obtaining mechanism (e.g. from globalmanagers)
    private static string UnityVersion
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get => Application.unityVersion;
    }

    /// <summary>
    ///     The assembly name of this loading system
    /// </summary>
    private readonly string CurrentAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;

    /// <summary>
    ///     The assembly version of this loading system
    /// </summary>
    private readonly Version CurrentAssemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
    
    internal static void StaticStart(string gameExePath = null)
    {
        try
        {
            if (staticStartHasBeenCalled)
            {
                Logger.Log(LogLevel.Fatal, "StaticStart called more than once");
                return;
            }

            staticStartHasBeenCalled = true;
            
            Logger.Log(LogLevel.Debug, "Entering chainloader StaticStart");

            Instance = new UnityChainloader();
            Instance.Initialize(gameExePath);
            PhaseManager.Instance.StartPhase(BepInPhases.AfterGameAssembliesLoaded);
            PhaseManager.Instance.StartPhase(BepInPhases.GameInitialised);

            Logger.Log(LogLevel.Debug, "Exiting chainloader StaticStart");
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Fatal, $"Unable to complete chainloader StaticStart: {ex.Message}");
            Logger.Log(LogLevel.Fatal, ex.StackTrace);
        }
    }

    internal override void Initialize(string gameExePath = null)
    {
        try
        {
            Logger.Log(LogLevel.Debug, "Entering chainloader initialize");

            Instance = this;

            UnityTomlTypeConverters.AddUnityEngineConverters();

            Logger.Log(LogLevel.Debug, "Initializing ThreadingHelper");
            ThreadingHelper.Initialize();

            Logger.Log(LogLevel.Debug, "Creating Manager object");
            ManagerObject = new GameObject("BepInEx_Manager") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(ManagerObject);

            Logger.Log(LogLevel.Debug, "Getting game product name");
            var productNameProp =
                typeof(Application).GetProperty("productName", BindingFlags.Public | BindingFlags.Static);
            _consoleTitle =
                $"{CurrentAssemblyName} {CurrentAssemblyVersion} - {productNameProp?.GetValue(null, null) ?? Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().ProcessName)}";

            Logger.Log(LogLevel.Debug, "Falling back to BaseChainloader initializer");

            base.Initialize(gameExePath);

            Logger.Log(LogLevel.Debug, "Exiting chainloader initialize");
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Fatal, $"Unable to complete chainloader init: {ex.Message}");
            Logger.Log(LogLevel.Fatal, ex.StackTrace);
        }
    }

    internal override void InitializeLoggers()
    {
        base.InitializeLoggers();

        Logger.Listeners.Add(new UnityLogListener());

        // Update version info from runtime in case it wasn't set yet
        var prevVersion = UnityInfo.Version;
        UnityInfo.SetRuntimeUnityVersion(UnityVersion);
        if (UnityInfo.Version != prevVersion)
            Logger.Log(LogLevel.Info, $"UnityPlayer version: {UnityInfo.Version}");

        if (!ConfigUnityLog.ConfigDiskWriteUnityLog.Value) DiskLogListener.BlacklistedSources.Add("Unity Log");

        ChainloaderLogHelper.RewritePreloaderLogs();

        if (ConfigUnityLog.ConfigUnityLogging.Value)
            Logger.Sources.Add(new UnityLogSource());
    }
}
