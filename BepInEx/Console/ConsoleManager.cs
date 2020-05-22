using System;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using BepInEx.Unix;

namespace BepInEx
{
	public static class ConsoleManager
	{
		internal static IConsoleDriver Driver { get; set; }

		/// <summary>
		/// True if an external console has been started, false otherwise.
		/// </summary>
		public static bool ConsoleActive => Driver?.ConsoleActive ?? false;

		/// <summary>
		/// The stream that writes to the standard out stream of the process. Should never be null.
		/// </summary>
		public static TextWriter StandardOutStream => Driver?.StandardOut;

		/// <summary>
		/// The stream that writes to an external console. Null if no such console exists
		/// </summary>
		public static TextWriter ConsoleStream => Driver?.ConsoleOut;


		public static void Initialize(bool alreadyActive)
		{
			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.MacOSX:
				case PlatformID.Unix:
				{
					Driver = new LinuxConsoleDriver();
					break;
				}

				case PlatformID.Win32NT:
				{
					Driver = new WindowsConsoleDriver();
					break;
				}
			}

			Driver.Initialize(alreadyActive);
		}

		private static void DriverCheck()
		{
			if (Driver == null)
				throw new InvalidOperationException("Driver has not been initialized");
		}


		public static void CreateConsole()
		{
			if (ConsoleActive)
				return;

			DriverCheck();

			Driver.CreateConsole();
			SetConsoleStreams();
		}

		public static void DetachConsole()
		{
			if (!ConsoleActive)
				return;

			DriverCheck();

			Driver.DetachConsole();
			SetConsoleStreams();
		}

		public static void SetConsoleEncoding()
		{
			// Apparently Windows code-pages work in Mono.
			// https://stackoverflow.com/a/33456543
			// Alternatively we can pass in "shift-jis"
			var encoding = ConfigConsoleShiftJis.Value ? Encoding.GetEncoding(932): Encoding.UTF8;

			SetConsoleEncoding(encoding);
		}

		public static void SetConsoleEncoding(Encoding encoding)
		{
			if (!ConsoleActive)
				throw new InvalidOperationException("Console is not currently active");

			DriverCheck();

			Driver.SetConsoleEncoding(encoding);
		}

		public static void SetConsoleTitle(string title)
		{
			DriverCheck();

			Driver.SetConsoleTitle(title);
		}

		public static void SetConsoleColor(ConsoleColor color)
		{
			DriverCheck();

			Driver.SetConsoleColor(color);
		}

		internal static void SetConsoleStreams()
		{
			if (ConsoleActive)
			{
				Console.SetOut(ConsoleStream);
				Console.SetError(ConsoleStream);
			}
			else
			{
				Console.SetOut(TextWriter.Null);
				Console.SetError(TextWriter.Null);
			}
		}

		public static readonly ConfigEntry<bool> ConfigConsoleEnabled = ConfigFile.CoreConfig.Bind(
			"Logging.Console", "Enabled",
			false,
			"Enables showing a console for log output.");

		public static readonly ConfigEntry<bool> ConfigConsoleShiftJis = ConfigFile.CoreConfig.Bind(
			"Logging.Console", "ShiftJisEncoding",
			false,
			"If true, console is set to the Shift-JIS encoding, otherwise UTF-8 encoding.");
	}
}
