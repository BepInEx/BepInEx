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

		public TtyInfo TtyInfo { get; private set; }

		public void Initialize(bool alreadyActive)
		{
			// Console is always considered active on Unix
			ConsoleActive = true;
			
			UseMonoTtyDriver = typeof(Console).Assembly.GetType("System.ConsoleDriver") != null;

			StdoutRedirected = UnixStreamHelper.isatty(1) != 1;

			var duplicateStream = UnixStreamHelper.CreateDuplicateStream(1);

			if (UseMonoTtyDriver && !StdoutRedirected)
			{
				// Mono implementation handles xterm for us

				var writer = ConsoleWriter.CreateConsoleStreamWriter(duplicateStream, Console.Out.Encoding, true);

				StandardOut = TextWriter.Synchronized(writer);

				var driver = AccessTools.Field(AccessTools.TypeByName("System.ConsoleDriver"), "driver").GetValue(null);
				AccessTools.Field(AccessTools.TypeByName("System.TermInfoDriver"), "stdout").SetValue(driver, writer);
			}
			else
			{
				// Handle TTY ourselves

				var writer = new StreamWriter(duplicateStream, Console.Out.Encoding);

				writer.AutoFlush = true;

				StandardOut = TextWriter.Synchronized(writer);

				TtyInfo = TtyHandler.GetTtyInfo();
			}

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
			if (StdoutRedirected)
				return;

			if (UseMonoTtyDriver)
			{
				// Use mono's inbuilt terminfo driver to set the foreground color for us
				SafeConsole.ForegroundColor = color;
			}
			else
			{
				ConsoleOut.Write(TtyInfo.GetAnsiCode(color));
			}
		}

		public void SetConsoleEncoding(Encoding encoding)
		{
			// We shouldn't be changing this on Unix
		}

		public void SetConsoleTitle(string title)
		{
			if (StdoutRedirected)
				return;

			if (UseMonoTtyDriver && SafeConsole.TitleExists)
			{
				SafeConsole.Title = title;
			}
			else
			{
				ConsoleOut.Write($"\u001B]2;{title.Replace("\\", "\\\\")}\u0007");
			}
		}
	}
}