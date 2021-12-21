using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Preloader.Core.Logging;
using BepInEx.Unity.Logging;
using MonoMod.Utils;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace BepInEx.Unity.Bootstrap;

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

    private string _consoleTitle;

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

    private static void StaticStart(string gameExePath = null)
    {
        var instance = new UnityChainloader();
        instance.Initialize(gameExePath);
        instance.Execute();
    }


    public override void Initialize(string gameExePath = null)
    {
        UnityTomlTypeConverters.AddUnityEngineConverters();

        ThreadingHelper.Initialize();

        ManagerObject = new GameObject("BepInEx_Manager") { hideFlags = HideFlags.HideAndDontSave };
        Object.DontDestroyOnLoad(ManagerObject);

        var productNameProp =
            typeof(Application).GetProperty("productName", BindingFlags.Public | BindingFlags.Static);
        _consoleTitle =
            $"{CurrentAssemblyName} {CurrentAssemblyVersion} - {productNameProp?.GetValue(null, null) ?? Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().ProcessName)}";

        base.Initialize(gameExePath);
    }

    protected override void InitializeLoggers()
    {
        base.InitializeLoggers();

        Logger.Listeners.Add(new UnityLogListener());

        if (!PlatformHelper.Is(Platform.Windows)) Logger.Log(LogLevel.Info, $"Detected Unity version: v{UnityVersion}");


        if (!ConfigDiskWriteUnityLog.Value) DiskLogListener.BlacklistedSources.Add("Unity Log");


        ChainloaderLogHelper.RewritePreloaderLogs();


        if (ConfigUnityLogging.Value)
            Logger.Sources.Add(new UnityLogSource());
    }

    public override BaseUnityPlugin LoadPlugin(PluginInfo pluginInfo, Assembly pluginAssembly) =>
        (BaseUnityPlugin) ManagerObject.AddComponent(pluginAssembly.GetType(pluginInfo.TypeName));
}
