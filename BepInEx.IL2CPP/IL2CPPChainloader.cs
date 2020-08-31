using System;
using System.Diagnostics;
using System.IO;
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

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate IntPtr RuntimeInvokeDetour(IntPtr method, IntPtr obj, IntPtr parameters, IntPtr exc);

		[DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

		private static RuntimeInvokeDetour originalInvoke;
		
		public unsafe IL2CPPChainloader()
		{
			UnityVersionHandler.Initialize(2019, 3, 15);
			File.AppendAllText("log.log", "Initialized unhollower\n");
			ClassInjector.DoHook = (ptr, intPtr) =>
			{
				var detour = new NativeDetour(new IntPtr(*((int**)ptr)), intPtr);
				detour.Apply();
			};

			foreach (var processModule in Process.GetCurrentProcess().Modules.Cast<ProcessModule>())
			{
				File.AppendAllText("wew.log", $"{processModule.ModuleName}\n");
			}
			
			var gameAssemblyModule = Process.GetCurrentProcess().Modules.Cast<ProcessModule>().First(x => x.ModuleName.Contains("GameAssembly"));
			File.AppendAllText("wew.log", $"Got module: {gameAssemblyModule.ModuleName}; addr: {gameAssemblyModule.BaseAddress}\n");
			var functionPtr = GetProcAddress(gameAssemblyModule.BaseAddress, "il2cpp_runtime_invoke"); //DynDll.GetFunction(gameAssemblyModule.BaseAddress, "il2cpp_runtime_invoke");

			File.AppendAllText("wew.log", $"Got fptr: {functionPtr}\n");
			
			// RuntimeInvokeDetour invokeHook = (method, obj, parameters, exc) =>
			// {
			// 	// UnityLogSource.LogInfo(Marshal.PtrToStringAnsi(UnhollowerBaseLib.IL2CPP.il2cpp_method_get_name(method)));
			// 	return originalInvoke(method, obj, parameters, exc);
			// };
			// UnhollowerBaseLib.IL2CPP.il2cpp_method_get_name(method)

			var invokeDetour = new NativeDetour(functionPtr, Marshal.GetFunctionPointerForDelegate(new RuntimeInvokeDetour(OnInvokeMethod)), new NativeDetourConfig {ManualApply = true});

			File.AppendAllText("log.log", "Got detour\n");
			originalInvoke = invokeDetour.GenerateTrampoline<RuntimeInvokeDetour>();
			File.AppendAllText("log.log", "Got trampoline\n");
			
			invokeDetour.Apply();
			File.AppendAllText("log.log", "Applied!\n");
		}

		private static IntPtr OnInvokeMethod(IntPtr method, IntPtr obj, IntPtr parameters, IntPtr exc)
		{
			lock (originalInvoke)
			{
				try
				{
					File.AppendAllText("log.log", $"Got call: {Marshal.PtrToStringAnsi(UnhollowerBaseLib.IL2CPP.il2cpp_method_get_name(method))}\n");
				}
				catch (Exception e)
				{
					File.AppendAllText("err.log", e.ToString() + "\n");
				}
			
				return originalInvoke(method, obj, parameters, exc);
			}
		}

		protected override void InitializeLoggers()
		{
			//Logger.Listeners.Add(new UnityLogListener());

			//if (ConfigUnityLogging.Value)
			//	Logger.Sources.Add(new UnityLogSource());

			Logger.Sources.Add(UnityLogSource);


			ManualLogSource unhollowerLogSource = Logger.CreateLogSource("Unhollower");

			UnhollowerBaseLib.LogSupport.InfoHandler += unhollowerLogSource.LogInfo;
			UnhollowerBaseLib.LogSupport.WarningHandler += unhollowerLogSource.LogWarning;
			UnhollowerBaseLib.LogSupport.TraceHandler += unhollowerLogSource.LogDebug;
			UnhollowerBaseLib.LogSupport.ErrorHandler += unhollowerLogSource.LogError;

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