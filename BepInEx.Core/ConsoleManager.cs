using System;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using UnityInjector.ConsoleUtil;

namespace BepInEx
{
	public static class ConsoleManager
	{
		public static bool ConsoleActive { get; private set; }

		public static TextWriter StandardOut => ConsoleWindow.StandardOut;

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
				default:
					throw new PlatformNotSupportedException("Spawning a console is not currently supported on this platform");
			}
		}

		public static void SetConsoleTitle(string title)
		{
			if (!ConsoleActive)
				throw new InvalidOperationException("Console is not currently active");

			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.Win32NT:
				{
					ConsoleWindow.Title = title;
					break;
				}
				default:
					throw new PlatformNotSupportedException("Spawning a console is not currently supported on this platform");
			}
		}

		public static void ForceSetActive(bool value)
		{
			ConsoleActive = value;
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
