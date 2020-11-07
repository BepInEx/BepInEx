using System;

namespace BepInEx.Logging
{
	/// <summary>
	/// Log source that can output log messages.
	/// </summary>
	public interface ILogSource : IDisposable
	{
		/// <summary>
		/// Name of the log source.
		/// </summary>
		string SourceName { get; }

		/// <summary>
		/// Event that sends the log message. Call <see cref="EventHandler.Invoke"/> to send a log message.
		/// </summary>
		event EventHandler<LogEventArgs> LogEvent;
	}
}