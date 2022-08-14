using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
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
public class UnityChainloader : BaseChainloader<BaseUnityPlugin>
{
    private static readonly ConfigEntry<bool> ConfigUnityLogging = ConfigFile.CoreConfig.Bind(
     "Logging", "UnityLogListening",
     true,
     "Enables showing unity log messages in the BepInEx logging system.");

    private static readonly ConfigEntry<bool> ConfigDiskWriteUnityLog = ConfigFile.CoreConfig.Bind(
     "Logging.Disk", "WriteUnityLog",
     false,
     "Include unity log messages in log file output.");

    private static readonly bool staticStartHasBeenCalled = false;

    private string _consoleTitle;

    // TODO: Remove once proper instance handling exists
    public static UnityChainloader Instance { get; set; }

    /// <summary>
    ///     The GameObject that all plugins are attached to as components.
    /// </summary>
    public static GameObject ManagerObject { get; private set; }

    protected override string ConsoleTitle => _consoleTitle;


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

    [Obsolete("This method is public due to a limitation with Unity 4.x. DO NOT CALL", true)]
    public static void StaticStart(string gameExePath = null)
    {
        try
        {
            if (staticStartHasBeenCalled)
                throw new InvalidOperationException("Cannot call StaticStart again");

            Logger.Log(LogLevel.Debug, "Entering chainloader StaticStart");

            var instance = new UnityChainloader();
            instance.Initialize(gameExePath);
            instance.Execute();

            Logger.Log(LogLevel.Debug, "Exiting chainloader StaticStart");
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Fatal, $"Unable to complete chainloader StaticStart: {ex.Message}");
            Logger.Log(LogLevel.Fatal, ex.StackTrace);
        }
    }

    public override void Initialize(string gameExePath = null)
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

    protected override void InitializeLoggers()
    {
        base.InitializeLoggers();

        Logger.Listeners.Add(new UnityLogListener());

        // Update version info from runtime in case it wasn't set yet
        var prevVersion = UnityInfo.Version;
        UnityInfo.SetRuntimeUnityVersion(UnityVersion);
        if (UnityInfo.Version != prevVersion)
            Logger.Log(LogLevel.Info, $"UnityPlayer version: {UnityInfo.Version}");

        if (!ConfigDiskWriteUnityLog.Value) DiskLogListener.BlacklistedSources.Add("Unity Log");

        ChainloaderLogHelper.RewritePreloaderLogs();

        if (ConfigUnityLogging.Value)
            Logger.Sources.Add(new UnityLogSource());
    }

    public override BaseUnityPlugin LoadPlugin(PluginInfo pluginInfo, Assembly pluginAssembly) =>
        (BaseUnityPlugin) ManagerObject.AddComponent(pluginAssembly.GetType(pluginInfo.TypeName));
}
