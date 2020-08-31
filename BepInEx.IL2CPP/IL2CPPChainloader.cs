using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using BepInEx.Preloader.Core;
using BepInEx.Preloader.Core.Logging;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using UnhollowerBaseLib.Runtime;
using UnhollowerRuntimeLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace BepInEx.IL2CPP
{
	public class IL2CPPChainloader : BaseChainloader<BasePlugin>
	{
		private static ManualLogSource UnityLogSource = new ManualLogSource("Unity");

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate void UnityLogCallbackDelegate([In] [MarshalAs(UnmanagedType.LPStr)] string log);
		private static void UnityLogCallback([In] [MarshalAs(UnmanagedType.LPStr)] string log)
		{
			UnityLogSource.LogInfo(log.Trim());
		}

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate IntPtr RuntimeInvokeDetour(IntPtr method, IntPtr obj, IntPtr parameters, IntPtr exc);

		public unsafe IL2CPPChainloader()
		{
			ClassInjector.DoHook = (ptr, intPtr) =>
			{
				var detour = new NativeDetour(new IntPtr(*((int**)ptr)), intPtr);
				detour.Apply();
			};

			var gameAssemblyModule = Process.GetCurrentProcess().Modules.Cast<ProcessModule>().First(x => x.FileName.Contains("GameAssembly"));
			var functionPtr = DynDll.GetFunction(gameAssemblyModule.BaseAddress, "il2cpp_runtime_invoke");

			RuntimeInvokeDetour originalInvoke = null;
			RuntimeInvokeDetour invokeHook = (method, obj, parameters, exc) =>
			{
				UnityLogSource.LogInfo(Marshal.PtrToStringAnsi(UnhollowerBaseLib.IL2CPP.il2cpp_method_get_name(method)));
				return originalInvoke(method, obj, parameters, exc);
			};

			var invokeDetour = new NativeDetour(functionPtr, Marshal.GetFunctionPointerForDelegate(invokeHook), new NativeDetourConfig {ManualApply = true});

			originalInvoke = invokeDetour.GenerateTrampoline<RuntimeInvokeDetour>();

			invokeDetour.Apply();


			//UnityVersionHandler.Initialize(2019, 3, 15);
		}

		protected override void InitializeLoggers()
		{
			//Logger.Listeners.Add(new UnityLogListener());

			//if (ConfigUnityLogging.Value)
			//	Logger.Sources.Add(new UnityLogSource());

			Logger.Sources.Add(UnityLogSource);


			ManualLogSource unhollowerLogSource = Logger.CreateLogSource("Unhollower");

			UnhollowerBaseLib.LogSupport.InfoHandler += s => unhollowerLogSource.LogInfo(s);
			UnhollowerBaseLib.LogSupport.WarningHandler += s => unhollowerLogSource.LogWarning(s);
			UnhollowerBaseLib.LogSupport.TraceHandler += s => unhollowerLogSource.LogDebug(s);
			UnhollowerBaseLib.LogSupport.ErrorHandler += s => unhollowerLogSource.LogError(s);

			base.InitializeLoggers();


			//if (!ConfigDiskWriteUnityLog.Value)
			//{
			//	DiskLogListener.BlacklistedSources.Add("Unity Log");
			//}


			foreach (var preloaderLogEvent in PreloaderConsoleListener.LogEvents)
			{
				PreloaderLogger.Log.Log(preloaderLogEvent.Level, preloaderLogEvent.Data);
			}

			//UnityEngine.Application.s_LogCallbackHandler = DelegateSupport.ConvertDelegate<Application.LogCallback>(new Action<string>(UnityLogCallback));
			//UnityEngine.Application.s_LogCallbackHandler = (Application.LogCallback)new Action<string>(UnityLogCallback);
			var loggerPointer = Marshal.GetFunctionPointerForDelegate(new UnityLogCallbackDelegate(UnityLogCallback));
			UnhollowerBaseLib.IL2CPP.il2cpp_register_log_callback(loggerPointer);
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