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

		public void CreateConsole()
		{
			// On some Unity mono builds the SafeFileHandle overload for FileStream is missing
			// so we use the older but always included one instead
#pragma warning disable 618
			ConsoleWindow.Attach();
			
			// If stdout exists, write to it, otherwise make it the same as console out
			// Not sure if this is needed? Does the original Console.Out still work?
			var stdout = ConsoleWindow.OriginalStdoutHandle != IntPtr.Zero ? ConsoleWindow.OriginalStdoutHandle : ConsoleWindow.ConsoleOutHandle;
			var originalOutStream = new FileStream(stdout, FileAccess.Write);
			StandardOut = new StreamWriter(originalOutStream, new UTF8Encoding(false))
			{
				AutoFlush = true
			};

			var consoleOutStream = new FileStream(ConsoleWindow.ConsoleOutHandle, FileAccess.Write);
			ConsoleOut = new StreamWriter(consoleOutStream, Console.OutputEncoding)
			{
				AutoFlush = true
			};
			ConsoleActive = true;
#pragma warning restore 618
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

		public void SetConsoleEncoding(Encoding encoding)
		{
			ConsoleEncoding.ConsoleCodePage = (uint)encoding.CodePage;
			Console.OutputEncoding = encoding;
		}

		public void SetConsoleTitle(string title)
		{
			ConsoleWindow.Title = title;
		}
	}
}