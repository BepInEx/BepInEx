using System;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using BepInEx.Unix;
using BepInEx.ConsoleUtil;
using HarmonyLib;
using UnityInjector.ConsoleUtil;

namespace BepInEx
{
	public static class ConsoleManager
	{
		/// <summary>
		/// True if an external console has been started, false otherwise.
		/// </summary>
		public static bool ConsoleActive { get; private set; }

		/// <summary>
		/// The stream that writes to the standard out stream of the process. Should never be null.
		/// </summary>
		public static TextWriter StandardOutStream { get; internal set; }

		/// <summary>
		/// The stream that writes to an external console. Null if no such console exists
		/// </summary>
		public static TextWriter ConsoleStream { get; internal set; }


		public static void Initialize(bool active)
		{
			StandardOutStream = Console.Out;
			Console.SetOut(StandardOutStream);
			ConsoleActive = active;

			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.MacOSX:
				case PlatformID.Unix:
				{
					ConsoleActive = true;

					var duplicateStream = UnixStreamHelper.CreateDuplicateStream(1);

					var writer = ConsoleWriter.CreateConsoleStreamWriter(duplicateStream, Console.Out.Encoding, true);
					
					StandardOutStream = TextWriter.Synchronized(writer);
					
					var driver = AccessTools.Field(AccessTools.TypeByName("System.ConsoleDriver"), "driver").GetValue(null);
					AccessTools.Field(AccessTools.TypeByName("System.TermInfoDriver"), "stdout").SetValue(driver, writer);

					Console.SetOut(StandardOutStream);
					break;
				}
			}
		}


		public static void CreateConsole()
		{
			if (ConsoleActive)
				return;
			
			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.Win32NT:
				{
					ConsoleWindow.Attach();
					break;
				}

				default:
					throw new PlatformNotSupportedException("Spawning a console is not currently supported on this platform");
			}

			SetConsoleStreams();

			ConsoleActive = true;
		}

		public static void DetachConsole()
		{
			if (!ConsoleActive)
				return;

			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.Win32NT:
				{
					ConsoleWindow.Detach();
					break;
				}

				default:
					throw new PlatformNotSupportedException("Spawning a console is not currently supported on this platform");
			}

			ConsoleActive = false;
		}

		public static void SetConsoleEncoding()
		{
			uint encoding = ConfigConsoleShiftJis.Value ? 932 : (uint)Encoding.UTF8.CodePage;

			SetConsoleEncoding(encoding);
		}

		public static void SetConsoleEncoding(uint encodingCodePage)
		{
			if (!ConsoleActive)
				throw new InvalidOperationException("Console is not currently active");

			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.Win32NT:
				{
					ConsoleEncoding.ConsoleCodePage = encodingCodePage;
					Console.OutputEncoding = Encoding.GetEncoding((int)encodingCodePage);
					break;
				}

				case PlatformID.MacOSX:
				case PlatformID.Unix:
				{
					break;
				}
			}
		}

		public static void SetConsoleTitle(string title)
		{
			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.Win32NT:
				{
					if (!ConsoleActive)
						return;

					ConsoleWindow.Title = title;
					break;
				}
			}
		}

		public static void SetConsoleColor(ConsoleColor color)
		{
			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.Win32NT:
				{
					if (!ConsoleActive)
						return;

					Kon.ForegroundColor = color;
					break;
				}
			}

			SafeConsole.ForegroundColor = color;
		}

		internal static void SetConsoleStreams()
		{
			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.Win32NT:
				{
					Console.SetOut(ConsoleStream);
					Console.SetError(ConsoleStream);
					break;
				}

				case PlatformID.MacOSX:
				case PlatformID.Unix:
				{
					// We do not have external consoles on Unix platforms.
					// Set the console output to standard output

					Console.SetOut(StandardOutStream);
					Console.SetError(StandardOutStream);
					break;
				}
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
