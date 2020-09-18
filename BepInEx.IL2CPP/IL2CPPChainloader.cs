using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Bootstrap;
using BepInEx.IL2CPP.Hook;
using BepInEx.Logging;
using BepInEx.Preloader.Core;
using BepInEx.Preloader.Core.Logging;
using HarmonyLib.Public.Patching;
using UnhollowerBaseLib.Runtime;
using UnhollowerRuntimeLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;
using BaseUnityEngine = UnityEngine;

namespace BepInEx.IL2CPP
{
	public class IL2CPPChainloader : BaseChainloader<BasePlugin>
	{
		private static ManualLogSource UnityLogSource = new ManualLogSource("Unity");

		public static void UnityLogCallback(string logLine, string exception, LogType type)
		{
			UnityLogSource.LogInfo(logLine.Trim());
		}

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr RuntimeInvokeDetourDelegate(IntPtr method, IntPtr obj, IntPtr parameters, IntPtr exc);

		[DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

		private static RuntimeInvokeDetourDelegate originalInvoke;

		private static FastNativeDetour RuntimeInvokeDetour { get; set; }

		private static IL2CPPChainloader Instance { get; set; }


		public override unsafe void Initialize(string gameExePath = null)
		{
			PatchManager.ResolvePatcher += IL2CPPDetourMethodPatcher.TryResolve;

			base.Initialize(gameExePath);
			Instance = this;

			var version = //Version.Parse(Application.unityVersion);
				Version.Parse(Process.GetCurrentProcess().MainModule.FileVersionInfo.FileVersion);

			UnityVersionHandler.Initialize(version.Major, version.Minor, version.Revision);

			// One or the other here for Unhollower to work correctly

			//ClassInjector.Detour = new DetourHandler();

			ClassInjector.DoHook = (ptr, patchedFunctionPtr) =>
			{
				IntPtr originalFunc = new IntPtr(*(void**)ptr);

				var detour = new FastNativeDetour(originalFunc, patchedFunctionPtr);
				
				detour.Apply();

				*(void**)ptr = (void*)detour.TrampolinePtr;
			};

			var gameAssemblyModule = Process.GetCurrentProcess().Modules.Cast<ProcessModule>().First(x => x.ModuleName.Contains("GameAssembly"));

			var functionPtr = GetProcAddress(gameAssemblyModule.BaseAddress, "il2cpp_runtime_invoke"); //DynDll.GetFunction(gameAssemblyModule.BaseAddress, "il2cpp_runtime_invoke");


			PreloaderLogger.Log.LogDebug($"Runtime invoke pointer: 0x{functionPtr.ToInt64():X}");

			RuntimeInvokeDetour = new FastNativeDetour(functionPtr,
				MonoExtensions.GetFunctionPointerForDelegate(new RuntimeInvokeDetourDelegate(OnInvokeMethod), CallingConvention.Cdecl));

			RuntimeInvokeDetour.Apply();

			originalInvoke = RuntimeInvokeDetour.GenerateTrampoline<RuntimeInvokeDetourDelegate>();

			PreloaderLogger.Log.LogDebug("Runtime invoke patched");
		}



		private static IntPtr OnInvokeMethod(IntPtr method, IntPtr obj, IntPtr parameters, IntPtr exc)
		{
			string methodName = Marshal.PtrToStringAnsi(UnhollowerBaseLib.IL2CPP.il2cpp_method_get_name(method));

			bool unhook = false;

			if (methodName == "Internal_ActiveSceneChanged")
			{
				try
				{
					//Application.s_LogCallbackHandler = new Action<string, string, LogType>(UnityLogCallback);

					//Application.CallLogCallback("test from OnInvokeMethod", "", LogType.Log, true);

					unhook = true;

					Instance.Execute();
				}
				catch (Exception ex)
				{
					UnityLogSource.LogError(ex);
				}
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
			//Logger.Listeners.Add(new UnityLogListener());

			//if (ConfigUnityLogging.Value)
			//	Logger.Sources.Add(new UnityLogSource());

			Logger.Sources.Add(UnityLogSource);

			base.InitializeLoggers();


			//if (!ConfigDiskWriteUnityLog.Value)
			//{
			//	DiskLogListener.BlacklistedSources.Add("Unity Log");
			//}




			// Temporarily disable the console log listener as we replay the preloader logs

			var logListener = Logger.Listeners.FirstOrDefault(logger => logger is ConsoleLogListener);

			if (logListener != null)
				Logger.Listeners.Remove(logListener);

			foreach (var preloaderLogEvent in PreloaderConsoleListener.LogEvents)
			{
				PreloaderLogger.Log.Log(preloaderLogEvent.Level, preloaderLogEvent.Data);
			}

			if (logListener != null)
				Logger.Listeners.Add(logListener);


			//UnityEngine.Application.s_LogCallbackHandler = DelegateSupport.ConvertDelegate<Application.LogCallback>(new Action<string>(UnityLogCallback));
			//UnityEngine.Application.s_LogCallbackHandler = (Application.LogCallback)new Action<string>(UnityLogCallback);

			//var loggerPointer = Marshal.GetFunctionPointerForDelegate(new UnityLogCallbackDelegate(UnityLogCallback));
			//UnhollowerBaseLib.IL2CPP.il2cpp_register_log_callback(loggerPointer);
		}

		public override BasePlugin LoadPlugin(PluginInfo pluginInfo, Assembly pluginAssembly)
		{
			var type = pluginAssembly.GetType(pluginInfo.TypeName);

			var pluginInstance = (BasePlugin)Activator.CreateInstance(type);

			pluginInstance.Load();

			return pluginInstance;
		}
	}
}