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

		void Initialize(bool alreadyActive);

		void CreateConsole();
		void DetachConsole();

		void SetConsoleColor(ConsoleColor color);
		void SetConsoleEncoding(Encoding encoding);
		void SetConsoleTitle(string title);
	}
}