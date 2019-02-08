using System;

namespace BepInEx.Logging
{
	public interface ILogSource : IDisposable
	{
		string SourceName { get; }

		event EventHandler<LogEventArgs> LogEvent;
	}
}