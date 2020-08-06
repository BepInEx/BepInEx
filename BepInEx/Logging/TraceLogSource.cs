using System.Diagnostics;

namespace BepInEx.Logging
{
	/// <summary>
	/// A source that routes all logs from <see cref="Trace"/> API to BepInEx logger.
	/// </summary>
	/// <inheritdoc cref="TraceListener"/>
	public class TraceLogSource : TraceListener
	{
		/// <summary>
		/// Whether Trace logs are rerouted.
		/// </summary>
		public static bool IsListening { get; protected set; } = false;

		private static TraceLogSource traceListener;

		/// <summary>
		/// Creates a new trace log source.
		/// </summary>
		/// <returns>New log source (or already existing one).</returns>
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

		/// <summary>
		/// Internal log source.
		/// </summary>
		protected ManualLogSource LogSource { get; }

		/// <summary>
		/// Creates a new trace log source.
		/// </summary>
		protected TraceLogSource()
		{
			LogSource = new ManualLogSource("Trace");
		}

		/// <summary>
		/// Writes a message to the underlying <see cref="ManualLogSource"/> instance.
		/// </summary>
		/// <param name="message">The message to write.</param>
		public override void Write(string message)
		{
			LogSource.LogInfo(message);
		}

		/// <summary>
		/// Writes a message and a newline to the underlying <see cref="ManualLogSource"/> instance.
		/// </summary>
		/// <param name="message">The message to write.</param>
		public override void WriteLine(string message)
		{
			LogSource.LogInfo(message);
		}

		/// <inheritdoc />
		public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
			=> TraceEvent(eventCache, source, eventType, id, string.Format(format, args));

		/// <inheritdoc />
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

			LogSource.Log(level, $"{message}".Trim());
		}
	}
}