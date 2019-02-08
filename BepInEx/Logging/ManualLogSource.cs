using System;

namespace BepInEx.Logging
{
	public class ManualLogSource : ILogSource
	{
		public string SourceName { get; }
		public event EventHandler<LogEventArgs> LogEvent;

		public ManualLogSource(string sourceName)
		{
			SourceName = sourceName;
		}

		public void Log(LogLevel level, object data)
		{
			LogEvent?.Invoke(this, new LogEventArgs(data, level, this));
		}

		public void LogFatal(object data) => Log(LogLevel.Fatal, data);
		public void LogError(object data) => Log(LogLevel.Error, data);
		public void LogWarning(object data) => Log(LogLevel.Warning, data);
		public void LogMessage(object data) => Log(LogLevel.Message, data);
		public void LogInfo(object data) => Log(LogLevel.Info, data);
		public void LogDebug(object data) => Log(LogLevel.Debug, data);

		public void Dispose() { }
	}
}