using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Harmony;

namespace BepInEx.Logging
{
	/// <summary>
	/// A trace listener that writes to an underlying <see cref="BaseLogger"/> instance.
	/// </summary>
	/// <inheritdoc cref="TraceListener"/>
	public class LoggerTraceListener : TraceListener
	{
		/// <summary>
		/// The logger instance that is being written to.
		/// </summary>
		public BaseLogger Logger { get; }

		static LoggerTraceListener()
		{
			try
			{
				TraceFixer.ApplyFix();
			}
			catch { } //ignore everything, if it's thrown an exception, we're using an assembly that has already fixed this
		}

		/// <param name="logger">The logger instance to write to.</param>
		public LoggerTraceListener(BaseLogger logger)
		{
			Logger = logger;
		}

		/// <summary>
		/// Writes a message to the underlying <see cref="BaseLogger"/> instance.
		/// </summary>
		/// <param name="message">The message to write.</param>
		public override void Write(string message)
		{
			Logger.Write(message);
		}

		/// <summary>
		/// Writes a message and a newline to the underlying <see cref="BaseLogger"/> instance.
		/// </summary>
		/// <param name="message">The message to write.</param>
		public override void WriteLine(string message)
		{
			Logger.WriteLine(message);
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

			Logger.Log(level, $"{source} : {message}");
		}

		/// <summary>
		/// This exists because the Mono implementation of <see cref="Trace"/> is/was broken, and would call Write directly instead of calling TraceEvent. This class fixes that with a <see cref="Harmony"/> hook.
		/// </summary>
		private static class TraceFixer
		{
			private static Type TraceImplType;

			private static object ListenersSyncRoot;
			private static TraceListenerCollection Listeners;
			private static PropertyInfo prop_AutoFlush;

			private static bool AutoFlush => (bool)prop_AutoFlush.GetValue(null, null);


			public static void ApplyFix()
			{
				TraceImplType = AppDomain.CurrentDomain.GetAssemblies()
										 .First(x => x.GetName().Name == "System")
										 .GetTypes()
										 .First(x => x.Name == "TraceImpl");


				ListenersSyncRoot = AccessTools.Property(TraceImplType, "ListenersSyncRoot").GetValue(null, null);

				Listeners = (TraceListenerCollection)AccessTools.Property(TraceImplType, "Listeners").GetValue(null, null);

				prop_AutoFlush = AccessTools.Property(TraceImplType, "AutoFlush");


				HarmonyInstance instance = HarmonyInstance.Create("com.bepis.bepinex.tracefix");

				instance.Patch(
					typeof(Trace).GetMethod("DoTrace", BindingFlags.Static | BindingFlags.NonPublic),
					new HarmonyMethod(typeof(TraceFixer).GetMethod(nameof(DoTraceReplacement), BindingFlags.Static | BindingFlags.Public)),
					null);
			}


			public static bool DoTraceReplacement(string kind, Assembly report, string message)
			{
				string arg = string.Empty;
				try
				{
					arg = report.GetName().Name;
				}
				catch (MethodAccessException) { }

				TraceEventType type = (TraceEventType)Enum.Parse(typeof(TraceEventType), kind);

				lock (ListenersSyncRoot)
				{
					foreach (object obj in Listeners)
					{
						TraceListener traceListener = (TraceListener)obj;
						traceListener.TraceEvent(new TraceEventCache(), arg, type, 0, message);

						if (AutoFlush)
						{
							traceListener.Flush();
						}
					}
				}

				return false;
			}
		}
	}
}