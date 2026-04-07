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
			// IMPORTANT: Resolve Unity's real UnityLogWriter type from CoreModule to avoid our stub.
			Type realUnityLogWriter = null;
			try
			{
				// typeof(UnityEngine.Debug).Assembly == UnityEngine.CoreModule
				var coreModule = typeof(UnityEngine.Debug).Assembly;
				realUnityLogWriter = coreModule.GetType("UnityEngine.UnityLogWriter", false);
			}
			catch
			{
				// ignore, we'll fall back below
			}

			// Probe the REAL engine type first (Unity 6+ has a managed WriteStringToUnityLog(string))
			if (realUnityLogWriter != null)
			{
				
				try
				{
					var writeMethod = realUnityLogWriter.GetMethod("WriteStringToUnityLog",
						BindingFlags.Static | BindingFlags.Public,
						null,
						new[] { typeof(string) },
						null);

					if (writeMethod != null)
					{
						// Harmless probe: if the icall/managed bridge isn't bound, this will throw.
						writeMethod.Invoke(null, new object[] { "" });

						WriteStringToUnityLog = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), writeMethod);
					}
				}
				catch
				{
					// fall through to legacy probing below
				}
			}

			// Legacy fallback: reflect over whatever UnityEngine.UnityLogWriter we see statically
			// (this will hit our stub on older Unity where it matched; safe no-op on Unity 6 if above succeeded)
			if (WriteStringToUnityLog == null)
			{
				foreach (MethodInfo methodInfo in typeof(UnityEngine.UnityLogWriter).GetMethods(BindingFlags.Static | BindingFlags.Public))
				{
					// We only want methods that take a single string, to avoid accidental binds.
					var ps = methodInfo.GetParameters();
					if (ps.Length != 1 || ps[0].ParameterType != typeof(string))
						continue;

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
			}

			// Fix for Unity 6. Both methods previously targeted no longer use internal calls
			if (WriteStringToUnityLog == null) 
			{
				try 
				{
					var type = Type.GetType("UnityEngine.UnityLogWriter, UnityEngine.CoreModule");
					var methodInfo = type.GetMethod("WriteStringToUnityLogImpl", 
						BindingFlags.Static | BindingFlags.NonPublic, 
						null, 
						new Type[] { typeof(string) }, 
						null);
					methodInfo.Invoke(null, new object[] { "" });
					WriteStringToUnityLog = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), methodInfo);
				} catch { }
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
	// Keep this stub for older engine versions / compatibility.
	// We intentionally do NOT try to reflect it first on Unity 6; the listener resolves the real type from CoreModule.
	internal sealed class UnityLogWriter
	{
		[MethodImpl(MethodImplOptions.InternalCall)]
		public static extern void WriteStringToUnityLogImpl(string s);

		[MethodImpl(MethodImplOptions.InternalCall)]
		public static extern void WriteStringToUnityLog(string s);
	}
}