using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BepInEx.Logging
{
	/// <summary>
	/// Logs entries using Unity specific outputs.
	/// </summary>
	public class UnityLogListener : ILogListener
	{
		internal static readonly Action<string> WriteStringToUnityLog;

		[DllImport("mono.dll", EntryPoint = "mono_lookup_internal_call")]
		private static extern IntPtr MonoLookupInternalCall(IntPtr gconstpointer);

		static UnityLogListener()
		{
			foreach (MethodInfo methodInfo in typeof(UnityEngine.UnityLogWriter).GetMethods(BindingFlags.Static | BindingFlags.Public))
			{
				if (MonoLookupInternalCall(methodInfo.MethodHandle.Value) == IntPtr.Zero)
					continue;

				WriteStringToUnityLog = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), methodInfo);
				break;
			}
		}

		public void LogEvent(object sender, LogEventArgs eventArgs)
		{
			if (sender is UnityLogSource)
				return;

			string log = $"[{eventArgs.Level}:{((ILogSource)sender).SourceName}] {eventArgs.Data}\r\n";

			WriteStringToUnityLog?.Invoke(log);
		}

		public void Dispose() { }
	}
}

namespace UnityEngine
{
	internal sealed class UnityLogWriter
	{
		[MethodImpl(MethodImplOptions.InternalCall)]
		public static extern void WriteStringToUnityLogImpl(string s);

		[MethodImpl(MethodImplOptions.InternalCall)]
		public static extern void WriteStringToUnityLog(string s);
	}
}