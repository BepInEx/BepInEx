extern alias il2cpp;
using System;
using BepInEx.Logging;
using il2cpp::UnityEngine;

namespace BepInEx.IL2CPP.Logging
{
	public class IL2CPPUnityLogSource : ILogSource
	{
		public string SourceName { get; } = "Unity";

		public event EventHandler<LogEventArgs> LogEvent;

		public void UnityLogCallback(string logLine, string exception, LogType type)
		{
			LogLevel level = LogLevel.Message;

			switch (type)
			{
				case LogType.Error:
					level = LogLevel.Error;
					break;
				case LogType.Assert:
					level = LogLevel.Debug;
					break;
				case LogType.Warning:
					level = LogLevel.Warning;
					break;
				case LogType.Log:
					level = LogLevel.Message;
					break;
				case LogType.Exception:
					level = LogLevel.Error;
					break;
			}

			LogEvent?.Invoke(this, new LogEventArgs(logLine, level, this));
		}

		public IL2CPPUnityLogSource()
		{
			Application.s_LogCallbackHandler = new Action<string, string, LogType>(UnityLogCallback);
		}

		public void Dispose() { }
	}
}