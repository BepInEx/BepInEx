using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using BepInEx.Preloader.Core;
using BepInEx.Preloader.Core.Logging;
using MonoMod.RuntimeDetour;
using UnhollowerBaseLib.Runtime;
using UnhollowerRuntimeLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace BepInEx.IL2CPP
{
	public class IL2CPPChainloader : BaseChainloader<BasePlugin>
	{
		private static ManualLogSource UnityLogSource = new ManualLogSource("Unity");

		public static void UnityLogCallback(string logLine, string exception, LogType type)
		{
			UnityLogSource.LogInfo(logLine.Trim());
		}


//		public const CallingConvention ArchConvention =
//#if X64
//			CallingConvention.Stdcall;
//#else
//			CallingConvention.Cdecl;
//#endif

		[UnmanagedFunctionPointer(CallingConvention.StdCall)]
		private delegate IntPtr RuntimeInvokeDetour(IntPtr method, IntPtr obj, IntPtr parameters, IntPtr exc);

		[DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

		private static RuntimeInvokeDetour originalInvoke;

		private static ManualLogSource unhollowerLogSource = Logger.CreateLogSource("Unhollower");

		private class DetourHandler : UnhollowerRuntimeLib.ManagedDetour
		{
			public T Detour<T>(IntPtr from, T to) where T : Delegate
			{
				var detour = new NativeDetour(from, to, new NativeDetourConfig { ManualApply = true });

				var trampoline = detour.GenerateTrampoline<T>();

				detour.Apply();

				return trampoline;
			}
		}


		public override unsafe void Initialize(string gameExePath = null)
		{
			base.Initialize(gameExePath);

			UnityVersionHandler.Initialize(2019, 2, 17);

			// One or the other here for Unhollower to work correctly

			//ClassInjector.Detour = new DetourHandler();

			ClassInjector.DoHook = (ptr, patchedFunctionPtr) =>
			{
				IntPtr originalFunc = new IntPtr(*(void**)ptr);

				var trampolinePtr = TrampolineGenerator.Generate(unhollowerLogSource, originalFunc, patchedFunctionPtr, out _);

				*(void**)ptr = (void*)trampolinePtr;
			};

			var gameAssemblyModule = Process.GetCurrentProcess().Modules.Cast<ProcessModule>().First(x => x.ModuleName.Contains("GameAssembly"));

			var functionPtr = GetProcAddress(gameAssemblyModule.BaseAddress, "il2cpp_runtime_invoke"); //DynDll.GetFunction(gameAssemblyModule.BaseAddress, "il2cpp_runtime_invoke");


			PreloaderLogger.Log.LogDebug($"Runtime invoke pointer: 0x{functionPtr.ToInt64():X}");

			var invokeDetour = new NativeDetour(functionPtr, Marshal.GetFunctionPointerForDelegate(new RuntimeInvokeDetour(OnInvokeMethod)));

			originalInvoke = invokeDetour.GenerateTrampoline<RuntimeInvokeDetour>();

			//invokeDetour.Apply(unhollowerLogSource);
			invokeDetour.Apply();
		}

		private static bool HasSet = false;

		private static HashSet<string> recordedNames = new HashSet<string>();
		private static IntPtr OnInvokeMethod(IntPtr method, IntPtr obj, IntPtr parameters, IntPtr exc)
		{
			string methodName = Marshal.PtrToStringAnsi(UnhollowerBaseLib.IL2CPP.il2cpp_method_get_name(method));
			IntPtr methodClass = UnhollowerBaseLib.IL2CPP.il2cpp_method_get_class(method);
			string methodClassName = Marshal.PtrToStringAnsi(UnhollowerBaseLib.IL2CPP.il2cpp_class_get_name(methodClass));
			string methodClassNamespace = Marshal.PtrToStringAnsi(UnhollowerBaseLib.IL2CPP.il2cpp_class_get_namespace(methodClass));

			string methodFullName = $"{methodClassNamespace}.{methodClassName}::{methodName}";

			if (!HasSet && methodName == "Internal_ActiveSceneChanged")
			{
				try
				{
					UnityEngine.Application.s_LogCallbackHandler = new Action<string, string, LogType>(UnityLogCallback);

					UnityLogSource.LogMessage($"callback set - {methodName}");

					UnityEngine.Application.CallLogCallback("test from OnInvokeMethod", "", LogType.Log, true);
				}
				catch (Exception ex)
				{
					UnityLogSource.LogError(ex);
				}

				HasSet = true;
			}

			var result = originalInvoke(method, obj, parameters, exc);

			//UnityLogSource.LogDebug(methodName + " => " + result.ToString("X"));

			if (!recordedNames.Contains(methodFullName))
			{
				UnityLogSource.LogDebug(methodFullName + " => " + result.ToString("X"));

				lock (recordedNames)
					recordedNames.Add(methodFullName);
			}

			return result;
		}

		protected override void InitializeLoggers()
		{
			//Logger.Listeners.Add(new UnityLogListener());

			//if (ConfigUnityLogging.Value)
			//	Logger.Sources.Add(new UnityLogSource());

			Logger.Sources.Add(UnityLogSource);



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