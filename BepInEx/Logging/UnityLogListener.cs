using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx.Configuration;

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

		/// <inheritdoc />
		public void LogEvent(object sender, LogEventArgs eventArgs)
		{
			if (eventArgs.Source is UnityLogSource)
				return;
			
			// Special case: don't write console twice since Unity can already do that
			if (LogConsoleToUnity.Value || eventArgs.Source.SourceName != "Console")
				WriteStringToUnityLog?.Invoke(eventArgs.ToStringLine());
		}

		/// <inheritdoc />
		public void Dispose() { }

		private ConfigEntry<bool> LogConsoleToUnity = ConfigFile.CoreConfig.Bind("Logging",
			"LogConsoleToUnityLog", false,
			new StringBuilder()
				.AppendLine("If enabled, writes Standard Output messages to Unity log")
				.AppendLine("NOTE: By default, Unity does so automatically. Only use this option if no console messages are visible in Unity log").ToString());
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