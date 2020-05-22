using System;
using System.IO;
using System.Text;
using BepInEx.Logging;
using HarmonyLib;
using UnityInjector.ConsoleUtil;

namespace BepInEx.Unix
{
	internal class LinuxConsoleDriver : IConsoleDriver
	{
		public TextWriter StandardOut { get; private set; }
		public TextWriter ConsoleOut { get; private set;  }

		public bool ConsoleActive { get; private set; }
		public bool ConsoleIsExternal => false;

		public bool UseMonoTtyDriver { get; private set; }
		public bool StdoutRedirected { get; private set; }

		public void Initialize(bool alreadyActive)
		{
			// Console is always considered active on Unix
			ConsoleActive = true;

			var duplicateStream = UnixStreamHelper.CreateDuplicateStream(1);

			var writer = ConsoleWriter.CreateConsoleStreamWriter(duplicateStream, Console.Out.Encoding, true);

			StandardOut = TextWriter.Synchronized(writer);

			var driver = AccessTools.Field(AccessTools.TypeByName("System.ConsoleDriver"), "driver").GetValue(null);
			AccessTools.Field(AccessTools.TypeByName("System.TermInfoDriver"), "stdout").SetValue(driver, writer);

			ConsoleOut = StandardOut;
		}

		public void CreateConsole()
		{
			Logger.LogWarning("An external console currently cannot be spawned on a Unix platform.");
		}

		public void DetachConsole()
		{
			throw new PlatformNotSupportedException("Cannot detach console on a Unix platform");
		}

		public void SetConsoleColor(ConsoleColor color)
		{
			if (SafeConsole.ForegroundColorExists)
			{
				// Use mono's inbuilt terminfo driver to set the foreground color for us
				SafeConsole.ForegroundColor = color;
			}
			else
			{
				throw new PlatformNotSupportedException("Cannot set Unix TTY color as mono implementation is missing");
			}
		}

		public void SetConsoleEncoding(Encoding encoding)
		{
			// We shouldn't be changing this on Unix
		}

		public void SetConsoleTitle(string title)
		{
			Console.Title = title;
		}
	}
}