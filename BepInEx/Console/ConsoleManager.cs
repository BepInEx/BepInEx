using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using BepInEx.Unix;
using MonoMod.Utils;

namespace BepInEx
{
	internal static class ConsoleManager
	{
		private const uint SHIFT_JIS_CP = 932;

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

		private static readonly bool? EnableConsoleArgOverride;

		private const string ENABLE_CONSOLE_ARG = "--enable-console";

		static ConsoleManager()
		{
			// Ensure GetCommandLineArgs failing (e.g. on unix) does not kill bepin
			try
			{
				string[] args = Environment.GetCommandLineArgs();
				for (var i = 0; i < args.Length; i++)
				{
					string res = args[i];
					if (res == ENABLE_CONSOLE_ARG && i + 1 < args.Length && bool.TryParse(args[i + 1], out bool enable))
						EnableConsoleArgOverride = enable;
				}
			}
			catch (Exception)
			{
				// Skip
			}
		}

		public static void Initialize(bool alreadyActive)
		{
			if (PlatformHelper.Is(Platform.Unix))
				Driver = new LinuxConsoleDriver();
			else if (PlatformHelper.Is(Platform.Windows))
				Driver = new WindowsConsoleDriver();

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

			// Apparently some versions of Mono throw a "Encoding name 'xxx' not supported"
			// if you use Encoding.GetEncoding
			// That's why we use of codepages directly and handle then in console drivers separately
			uint codepage = ConfigConsoleShiftJis.Value ? SHIFT_JIS_CP : (uint)Encoding.UTF8.CodePage;

			Driver.CreateConsole(codepage);
			
			if (ConfigPreventClose.Value)
				Driver.PreventClose();
		}

		public static void DetachConsole()
		{
			if (!ConsoleActive)
				return;

			DriverCheck();

			Driver.DetachConsole();
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

		public static bool ConsoleEnabled => EnableConsoleArgOverride ?? ConfigConsoleEnabled.Value;

		public static readonly ConfigEntry<bool> ConfigConsoleEnabled = ConfigFile.CoreConfig.Bind(
			"Logging.Console", "Enabled",
			false,
			"Enables showing a console for log output.");
		
		public static readonly ConfigEntry<bool> ConfigPreventClose = ConfigFile.CoreConfig.Bind(
			"Logging.Console", "PreventClose",
			false,
			"If enabled, will prevent closing the console (either by deleting the close button or in other platform-specific way).");

		public static readonly ConfigEntry<bool> ConfigConsoleShiftJis = ConfigFile.CoreConfig.Bind(
			"Logging.Console", "ShiftJisEncoding",
			false,
			"If true, console is set to the Shift-JIS encoding, otherwise UTF-8 encoding.");

		public static readonly ConfigEntry<ConsoleOutRedirectType> ConfigConsoleOutRedirectType = ConfigFile.CoreConfig.Bind(
			"Logging.Console", "StandardOutType",
			ConsoleOutRedirectType.Auto,
			new StringBuilder()
				.AppendLine("Hints console manager on what handle to assign as StandardOut. Possible values:")
				.AppendLine("Auto - lets BepInEx decide how to redirect console output")
				.AppendLine("ConsoleOut - prefer redirecting to console output; if possible, closes original standard output")
				.AppendLine("StandardOut - prefer redirecting to standard output; if possible, closes console out")
				.ToString()
		);

		public enum ConsoleOutRedirectType
		{
			[Description("Auto")]
			Auto = 0,

			[Description("Console Out")]
			ConsoleOut,

			[Description("Standard Out")]
			StandardOut
		}
	}
}
