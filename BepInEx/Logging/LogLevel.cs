using System;

namespace BepInEx.Logging
{
	/// <summary>
	/// The level, or severity of a log entry.
	/// </summary>
	[Flags]
	public enum LogLevel
	{
		/// <summary>
		/// No level selected.
		/// </summary>
		None = 0,

		/// <summary>
		/// A fatal error has occurred, which cannot be recovered from.
		/// </summary>
		Fatal = 1,

		/// <summary>
		/// An error has occured, but can be recovered from.
		/// </summary>
		Error = 2,

		/// <summary>
		/// A warning has been produced, but does not necessarily mean that something wrong has happened.
		/// </summary>
		Warning = 4,

		/// <summary>
		/// An important message that should be displayed to the user.
		/// </summary>
		Message = 8,

		/// <summary>
		/// A message of low importance.
		/// </summary>
		Info = 16,

		/// <summary>
		/// A message that would likely only interest a developer.
		/// </summary>
		Debug = 32,

		/// <summary>
		/// All log levels.
		/// </summary>
		All = Fatal | Error | Warning | Message | Info | Debug
	}

	/// <summary>
	/// Helper methods for log level handling.
	/// </summary>
	public static class LogLevelExtensions
	{
		/// <summary>
		/// Gets the highest log level when there could potentially be multiple levels provided.
		/// </summary>
		/// <param name="levels">The log level(s).</param>
		/// <returns>The highest log level supplied.</returns>
		public static LogLevel GetHighestLevel(this LogLevel levels)
		{
			var enums = Enum.GetValues(typeof(LogLevel));
			Array.Sort(enums);

			foreach (LogLevel e in enums)
			{
				if ((levels & e) != LogLevel.None)
					return e;
			}

			return LogLevel.None;
		}

		/// <summary>
		/// Returns a translation of a log level to it's associated console colour.
		/// </summary>
		/// <param name="level">The log level(s).</param>
		/// <returns>A console color associated with the highest log level supplied.</returns>
		public static ConsoleColor GetConsoleColor(this LogLevel level)
		{
			level = GetHighestLevel(level);

			switch (level)
			{
				case LogLevel.Fatal:
					return ConsoleColor.Red;
				case LogLevel.Error:
					return ConsoleColor.DarkRed;
				case LogLevel.Warning:
					return ConsoleColor.Yellow;
				case LogLevel.Message:
					return ConsoleColor.White;
				case LogLevel.Info:
				default:
					return ConsoleColor.Gray;
				case LogLevel.Debug:
					return ConsoleColor.DarkGray;
			}
		}
	}
}