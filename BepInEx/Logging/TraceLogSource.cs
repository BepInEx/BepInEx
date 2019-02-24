using System.Diagnostics;

namespace BepInEx.Logging
{
	/// <summary>
	/// A trace listener that writes to an underlying <see cref="BaseLogger"/> instance.
	/// </summary>
	/// <inheritdoc cref="TraceListener"/>
	public class TraceLogSource : TraceListener
	{
		public static bool IsListening { get; protected set; } = false;

		private static TraceLogSource traceListener;

		public static ILogSource CreateSource()
		{
			if (traceListener == null)
			{
				traceListener = new TraceLogSource();
				Trace.Listeners.Add(traceListener);
				IsListening = true;
			}

			return traceListener.LogSource;
		}

		protected ManualLogSource LogSource { get; }

		/// <param name="logger">The logger instance to write to.</param>
		protected TraceLogSource()
		{
			LogSource = new ManualLogSource("Trace");
		}

		/// <summary>
		/// Writes a message to the underlying <see cref="BaseLogger"/> instance.
		/// </summary>
		/// <param name="message">The message to write.</param>
		public override void Write(string message)
		{
			LogSource.Log(LogLevel.None, message);
		}

		/// <summary>
		/// Writes a message and a newline to the underlying <see cref="BaseLogger"/> instance.
		/// </summary>
		/// <param name="message">The message to write.</param>
		public override void WriteLine(string message)
		{
			LogSource.Log(LogLevel.None, $"{message}\r\n");
		}

		public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
			=> TraceEvent(eventCache, source, eventType, id, string.Format(format, args));

		public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
		{
			LogLevel level;

			switch (eventType)
			{
				case TraceEventType.Critical:
					level = LogLevel.Fatal;
					break;
				case TraceEventType.Error:
					level = LogLevel.Error;
					break;
				case TraceEventType.Warning:
					level = LogLevel.Warning;
					break;
				case TraceEventType.Information:
					level = LogLevel.Info;
					break;
				case TraceEventType.Verbose:
				default:
					level = LogLevel.Debug;
					break;
			}
			
			LogSource.Log(level, $"{message}");
		}
	}
}