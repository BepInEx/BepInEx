using System;

namespace BepInEx.Logging
{
	public interface ILogListener : IDisposable
	{
		void LogEvent(object sender, LogEventArgs eventArgs);
	}
}