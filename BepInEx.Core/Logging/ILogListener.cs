using System;

namespace BepInEx.Logging
{
	/// <summary>
	/// A generic log listener that receives log events and can route them to some output (e.g. file, console, socket).
	/// </summary>
	public interface ILogListener : IDisposable
	{
		/// <summary>
		/// Handle an incoming log event.
		/// </summary>
		/// <param name="sender">Log source that sent the event. Don't use; instead use <see cref="LogEventArgs.Source"/></param>
		/// <param name="eventArgs">Information about the log message.</param>
		void LogEvent(object sender, LogEventArgs eventArgs);
	}
}