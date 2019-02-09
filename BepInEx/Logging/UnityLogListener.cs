using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BepInEx.Logging
{
	/// <summary>
	/// Logs entries using Unity specific outputs.
	/// </summary>
	public class UnityLogListener : ILogListener
	{
		internal static readonly Action<string> WriteStringToUnityLog;

		static UnityLogListener()
		{
			foreach (MethodInfo methodInfo in typeof(UnityEngine.UnityLogWriter).GetMethods(BindingFlags.Static | BindingFlags.Public))
			{
				try
				{
					methodInfo.Invoke(null, new object[] { "" });
				}
				catch
				{
					continue;
				}

				WriteStringToUnityLog = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), methodInfo);
				break;
			}
			
			if (WriteStringToUnityLog == null)
				Logger.LogError("Unable to start Unity log writer");
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