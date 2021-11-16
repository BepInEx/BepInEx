﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.IL2CPP.Hook;
using BepInEx.IL2CPP.Logging;
using BepInEx.IL2CPP.Utils;
using BepInEx.Logging;
using BepInEx.Preloader.Core;
using BepInEx.Preloader.Core.Logging;
using HarmonyLib.Public.Patching;
using Il2Cpp.TlsAdapter;
using MonoMod.Utils;
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace BepInEx.IL2CPP
{
    public class IL2CPPChainloader : BaseChainloader<BasePlugin>
    {
        private static RuntimeInvokeDetourDelegate originalInvoke;
        private static InstallUnityTlsInterfaceDelegate originalInstallUnityTlsInterface;

        private static readonly ConfigEntry<bool> ConfigUnityLogging = ConfigFile.CoreConfig.Bind(
         "Logging", "UnityLogListening",
         true,
         "Enables showing unity log messages in the BepInEx logging system.");

        private static readonly ConfigEntry<bool> ConfigDiskWriteUnityLog = ConfigFile.CoreConfig.Bind(
         "Logging.Disk", "WriteUnityLog",
         false,
         "Include unity log messages in log file output.");

        private static FastNativeDetour RuntimeInvokeDetour { get; set; }
        private static FastNativeDetour InstallUnityTlsInterfaceDetour { get; set; }

        public static IL2CPPChainloader Instance { get; set; }

        /// <summary>
        /// Register and add a Unity Component (for example MonoBehaviour) into BepInEx global manager.
        ///
        /// Automatically registers the type with Il2Cpp type system if it isn't initialised already.
        /// </summary>
        /// <typeparam name="T">Type of the component to add.</typeparam>
        public static T AddUnityComponent<T>() where T : Il2CppObjectBase => AddUnityComponent(typeof(T)).Cast<T>();

        /// <summary>
        /// Register and add a Unity Component (for example MonoBehaviour) into BepInEx global manager.
        ///
        /// Automatically registers the type with Il2Cpp type system if it isn't initialised already.
        /// </summary>
        /// <param name="t">Type of the component to add</param>
        public static Il2CppObjectBase AddUnityComponent(Type t) => Il2CppUtils.AddComponent(t);

        public override void Initialize(string gameExePath = null)
        {
            GeneratedDatabasesUtil.DatabasesLocationOverride = Preloader.IL2CPPUnhollowedPath;
            PatchManager.ResolvePatcher += IL2CPPDetourMethodPatcher.TryResolve;

            base.Initialize(gameExePath);
            Instance = this;

            ClassInjector.Detour = new UnhollowerDetourHandler();

            var gameAssemblyModule = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
                                            .FirstOrDefault(x => x.ModuleName.Contains("GameAssembly") ||
                                                                 x.ModuleName.Contains("UserAssembly"));

            if (gameAssemblyModule == null)
            {
                Logger.LogFatal("Could not locate Il2Cpp game assembly (GameAssembly.dll) or (UserAssembly.dll). The game might be obfuscated or use a yet unsupported build of Unity.");
                return;
            }

            gameAssemblyModule.BaseAddress.TryGetFunction("il2cpp_runtime_invoke", out var runtimeInvokePtr);
            PreloaderLogger.Log.LogDebug($"Runtime invoke pointer: 0x{runtimeInvokePtr.ToInt64():X}");
            RuntimeInvokeDetour =
                FastNativeDetour.CreateAndApply(runtimeInvokePtr, OnInvokeMethod, out originalInvoke,
                                                CallingConvention.Cdecl);

            if (gameAssemblyModule.BaseAddress.TryGetFunction("il2cpp_unity_install_unitytls_interface",
                                                              out var installTlsPtr))
                InstallUnityTlsInterfaceDetour =
                    FastNativeDetour.CreateAndApply(installTlsPtr, OnInstallUnityTlsInterface,
                                                    out originalInstallUnityTlsInterface, CallingConvention.Cdecl);

            Logger.LogDebug("Initializing TLS adapters");
            Il2CppTlsAdapter.Initialize();

            PreloaderLogger.Log.LogDebug("Runtime invoke patched");
        }

        private void OnInstallUnityTlsInterface(IntPtr unityTlsInterfaceStruct)
        {
            Logger.LogDebug($"Captured UnityTls interface at {unityTlsInterfaceStruct.ToInt64():x8}");
            Il2CppTlsAdapter.Options.UnityTlsInterface = unityTlsInterfaceStruct;
            originalInstallUnityTlsInterface(unityTlsInterfaceStruct);
            InstallUnityTlsInterfaceDetour.Dispose();
            InstallUnityTlsInterfaceDetour = null;
        }

        private static IntPtr OnInvokeMethod(IntPtr method, IntPtr obj, IntPtr parameters, IntPtr exc)
        {
            var methodName = Marshal.PtrToStringAnsi(UnhollowerBaseLib.IL2CPP.il2cpp_method_get_name(method));

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

                    Instance.Execute();
                }
                catch (Exception ex)
                {
                    Logger.LogFatal("Unable to execute IL2CPP chainloader");
                    Logger.LogError(ex);
                }

            var result = originalInvoke(method, obj, parameters, exc);

            if (unhook)
            {
                RuntimeInvokeDetour.Dispose();

                PreloaderLogger.Log.LogDebug("Runtime invoke unpatched");
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
            foreach(var type in pluginAssembly.DefinedTypes)
            {
                if(typeof(Il2CppObjectBase).IsAssignableFrom(type) && !ClassInjector.IsTypeRegisteredInIl2Cpp(type))
                {
                    ClassInjector.RegisterTypeInIl2Cpp(type);
                }
            }

            var pluginType = pluginAssembly.GetType(pluginInfo.TypeName);

            var pluginInstance = (BasePlugin)Activator.CreateInstance(pluginType);

            pluginInstance.Load();

            return pluginInstance;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr RuntimeInvokeDetourDelegate(IntPtr method, IntPtr obj, IntPtr parameters, IntPtr exc);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void InstallUnityTlsInterfaceDelegate(IntPtr unityTlsInterfaceStruct);
    }
}
