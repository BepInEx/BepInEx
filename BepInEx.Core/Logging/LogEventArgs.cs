using System;

namespace BepInEx.Logging
{
	public class LogEventArgs : EventArgs
	{
		public object Data { get; protected set; }

		public LogLevel Level { get; protected set; }

		public ILogSource Source { get; protected set; }

		public LogEventArgs(object data, LogLevel level, ILogSource source)
		{
			Data = data;
			Level = level;
			Source = source;
		}
	}
}