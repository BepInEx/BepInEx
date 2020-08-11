using System;
using System.IO;
using System.Text;
using BepInEx.ConsoleUtil;
using UnityInjector.ConsoleUtil;

namespace BepInEx
{
	internal class WindowsConsoleDriver : IConsoleDriver
	{
		public TextWriter StandardOut { get; private set; }
		public TextWriter ConsoleOut { get; private set; }

		public bool ConsoleActive { get; private set; }
		public bool ConsoleIsExternal => true;

		public void Initialize(bool alreadyActive)
		{
			ConsoleActive = alreadyActive;

			StandardOut = Console.Out;
		}

		public void CreateConsole(uint codepage)
		{
			// On some Unity mono builds the SafeFileHandle overload for FileStream is missing
			// so we use the older but always included one instead
#pragma warning disable 618
			ConsoleWindow.Attach();

			// Make sure of ConsoleEncoding helper class because on some Monos
			// Encoding.GetEncoding throws NotImplementedException on most codepages
			// NOTE: We don't set Console.OutputEncoding because it resets any existing Console.Out writers
			ConsoleEncoding.ConsoleCodePage = codepage;
			
			// If stdout exists, write to it, otherwise make it the same as console out
			// Not sure if this is needed? Does the original Console.Out still work?
			var stdout = GetOutHandle();
			if (stdout == IntPtr.Zero)
			{
				StandardOut = TextWriter.Null;
				ConsoleOut = TextWriter.Null;
				return;
			}
			
			var originalOutStream = new FileStream(stdout, FileAccess.Write);
			StandardOut = new StreamWriter(originalOutStream, new UTF8Encoding(false))
			{
				AutoFlush = true
			};

			var consoleOutStream = new FileStream(ConsoleWindow.ConsoleOutHandle, FileAccess.Write);
			// Can't use Console.OutputEncoding because it can be null (i.e. not preference by user)
			ConsoleOut = new StreamWriter(consoleOutStream, ConsoleEncoding.OutputEncoding)
			{
				AutoFlush = true
			};
			ConsoleActive = true;
#pragma warning restore 618
		}

		private IntPtr GetOutHandle()
		{
			switch (ConsoleManager.ConfigConsoleOutRedirectType.Value)
			{
				case ConsoleManager.ConsoleOutRedirectType.ConsoleOut:
					return ConsoleWindow.ConsoleOutHandle;
				case ConsoleManager.ConsoleOutRedirectType.StandardOut:
					return ConsoleWindow.OriginalStdoutHandle;
				case ConsoleManager.ConsoleOutRedirectType.Auto:
				default:
					return ConsoleWindow.OriginalStdoutHandle != IntPtr.Zero ? ConsoleWindow.OriginalStdoutHandle : ConsoleWindow.ConsoleOutHandle;
			}
		}

		public void DetachConsole()
		{
			ConsoleWindow.Detach();

			ConsoleOut.Close();
			ConsoleOut = null;

			ConsoleActive = false;
		}

		public void SetConsoleColor(ConsoleColor color)
		{
			SafeConsole.ForegroundColor = color;
			Kon.ForegroundColor = color;
		}

		public void SetConsoleTitle(string title)
		{
			ConsoleWindow.Title = title;
		}
	}
}