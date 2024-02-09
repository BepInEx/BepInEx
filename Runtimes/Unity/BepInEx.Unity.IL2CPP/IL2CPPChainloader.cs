using System;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Preloader.Core;
using BepInEx.Preloader.Core.Logging;
using BepInEx.Unity.IL2CPP.Hook;
using BepInEx.Unity.IL2CPP.Logging;
using BepInEx.Unity.IL2CPP.Utils;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace BepInEx.Unity.IL2CPP;

public class IL2CPPChainloader : BaseChainloader<BasePlugin>
{
    private static RuntimeInvokeDetourDelegate originalInvoke;

    private static readonly ConfigEntry<bool> ConfigUnityLogging = ConfigFile.CoreConfig.Bind(
     "Logging", "UnityLogListening",
     true,
     "Enables showing unity log messages in the BepInEx logging system.");

    private static readonly ConfigEntry<bool> ConfigDiskWriteUnityLog = ConfigFile.CoreConfig.Bind(
     "Logging.Disk", "WriteUnityLog",
     false,
     "Include unity log messages in log file output.");


    private static INativeDetour RuntimeInvokeDetour { get; set; }

    public static IL2CPPChainloader Instance { get; set; }

    /// <summary>
    ///     Register and add a Unity Component (for example MonoBehaviour) into BepInEx global manager.
    ///     Automatically registers the type with Il2Cpp type system if it isn't initialised already.
    /// </summary>
    /// <typeparam name="T">Type of the component to add.</typeparam>
    public static T AddUnityComponent<T>() where T : Il2CppObjectBase => AddUnityComponent(typeof(T)).Cast<T>();

    /// <summary>
    ///     Register and add a Unity Component (for example MonoBehaviour) into BepInEx global manager.
    ///     Automatically registers the type with Il2Cpp type system if it isn't initialised already.
    /// </summary>
    /// <param name="t">Type of the component to add</param>
    public static Il2CppObjectBase AddUnityComponent(Type t) => Il2CppUtils.AddComponent(t);

    /// <summary>
    ///     Occurs after a plugin is instantiated and just before <see cref="BasePlugin.Load"/> is called.
    /// </summary>
    public event Action<PluginInfo, Assembly, BasePlugin> PluginLoad;

    public override void Initialize(string gameExePath = null)
    {
        base.Initialize(gameExePath);
        Instance = this;

        if (!NativeLibrary.TryLoad("GameAssembly", typeof(IL2CPPChainloader).Assembly, null, out var il2CppHandle))
        {
            Logger.Log(LogLevel.Fatal,
                       "Could not locate Il2Cpp game assembly (GameAssembly.dll, UserAssembly.dll or libil2cpp.so). The game might be obfuscated or use a yet unsupported build of Unity.");
            return;
        }

        var runtimeInvokePtr = NativeLibrary.GetExport(il2CppHandle, "il2cpp_runtime_invoke");
        PreloaderLogger.Log.Log(LogLevel.Debug, $"Runtime invoke pointer: 0x{runtimeInvokePtr.ToInt64():X}");
        RuntimeInvokeDetourDelegate invokeMethodDetour = OnInvokeMethod;

        RuntimeInvokeDetour =
            INativeDetour.CreateAndApply(runtimeInvokePtr, invokeMethodDetour, out originalInvoke);
        PreloaderLogger.Log.Log(LogLevel.Debug, "Runtime invoke patched");
    }

    private static IntPtr OnInvokeMethod(IntPtr method, IntPtr obj, IntPtr parameters, IntPtr exc)
    {
        var methodName = Marshal.PtrToStringAnsi(Il2CppInterop.Runtime.IL2CPP.il2cpp_method_get_name(method));

        var unhook = false;

        if (methodName == "Internal_ActiveSceneChanged")
            try
            {
                if (ConfigUnityLogging.Value)
                {
                    Logger.Sources.Add(new IL2CPPUnityLogSource());

                    Application.CallLogCallback("Test call after applying unity logging hook", "", LogType.Assert,
                                                true);
                }

                unhook = true;

                Il2CppInteropManager.PreloadInteropAssemblies();

                Instance.Execute();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Fatal, "Unable to execute IL2CPP chainloader");
                Logger.Log(LogLevel.Error, ex);
            }

        var result = originalInvoke(method, obj, parameters, exc);

        if (unhook)
        {
            RuntimeInvokeDetour.Dispose();

            PreloaderLogger.Log.Log(LogLevel.Debug, "Runtime invoke unpatched");
        }

        return result;
    }

    protected override void InitializeLoggers()
    {
        base.InitializeLoggers();

        if (!ConfigDiskWriteUnityLog.Value) DiskLogListener.BlacklistedSources.Add("Unity");

        ChainloaderLogHelper.RewritePreloaderLogs();

        Logger.Sources.Add(new IL2CPPLogSource());
    }

    public override BasePlugin LoadPlugin(PluginInfo pluginInfo, Assembly pluginAssembly)
    {
        var type = pluginAssembly.GetType(pluginInfo.TypeName);

        var pluginInstance = (BasePlugin) Activator.CreateInstance(type);

        PluginLoad?.Invoke(pluginInfo, pluginAssembly, pluginInstance);
        pluginInstance.Load();

        return pluginInstance;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr RuntimeInvokeDetourDelegate(IntPtr method, IntPtr obj, IntPtr parameters, IntPtr exc);
}
