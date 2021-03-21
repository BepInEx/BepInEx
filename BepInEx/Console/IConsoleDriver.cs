using System;
using System.IO;
using System.Text;

namespace BepInEx
{
	internal interface IConsoleDriver
	{
		TextWriter StandardOut { get; }
		TextWriter ConsoleOut { get; }

		bool ConsoleActive { get; }
		bool ConsoleIsExternal { get; }

		void PreventClose();
		
		void Initialize(bool alreadyActive);
		
		// Apparently Windows code-pages work in Mono.
		// https://stackoverflow.com/a/33456543
		void CreateConsole(uint codepage);
		void DetachConsole();

		void SetConsoleColor(ConsoleColor color);
		
		void SetConsoleTitle(string title);
		
		
	}
}