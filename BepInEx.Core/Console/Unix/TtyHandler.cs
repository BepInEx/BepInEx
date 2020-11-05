// Sections of this code have been abridged from https://github.com/mono/mono/blob/master/mcs/class/corlib/System/TermInfoReader.cs under the MIT license

using System;
using System.IO;
using System.Linq;
using System.Text;

namespace BepInEx.Unix
{
	internal class TtyInfo
	{
		public string TerminalType { get; set; } = "default";

		public int MaxColors { get; set; }

		public string[] ForegroundColorStrings { get; set; }

		public static TtyInfo Default { get; } = new TtyInfo
		{
			MaxColors = 0
		};

		public string GetAnsiCode(ConsoleColor color)
		{
			if (MaxColors <= 0 || ForegroundColorStrings == null)
				return string.Empty;

			int index = (int)color % MaxColors;
			return ForegroundColorStrings[index];
		}
	}

	internal static class TtyHandler
	{
		private static readonly string[] ncursesLocations = new[]
		{
			"/usr/share/terminfo",
			"/etc/terminfo",
			"/usr/lib/terminfo",
			"/lib/terminfo"
		};

		private static string TryTermInfoDir(string dir, string term)
		{
			string infoFilePath = $"{dir}/{(int)term[0]:x}/{term}";

			if (File.Exists(infoFilePath))
				return infoFilePath;

			infoFilePath = Utility.CombinePaths(dir, term.Substring(0, 1), term);

			if (File.Exists(infoFilePath))
				return infoFilePath;

			return null;
		}

		private static string FindTermInfoPath(string term)
		{
			if (string.IsNullOrEmpty(term))
				return null;

			string termInfoVar = Environment.GetEnvironmentVariable("TERMINFO");
			if (termInfoVar != null && Directory.Exists(termInfoVar))
			{
				string text = TryTermInfoDir(termInfoVar, term);
				if (text != null)
				{
					return text;
				}
			}

			foreach (string location in ncursesLocations)
			{
				if (Directory.Exists(location))
				{
					string text = TryTermInfoDir(location, term);

					if (text != null)
						return text;
				}
			}

			return null;
		}

		public static TtyInfo GetTtyInfo(string terminal = null)
		{
			terminal = terminal ?? Environment.GetEnvironmentVariable("TERM");
			var path = FindTermInfoPath(terminal);

			if (path == null)
				return TtyInfo.Default;

			byte[] buffer = File.ReadAllBytes(path);

			var info = TtyInfoParser.Parse(buffer);
			info.TerminalType = terminal;

			return info;
		}
	}

	internal static class TtyInfoParser
	{
		private static readonly int[] ansiColorMapping =
		{
			0, 4, 2, 6, 1, 5, 3, 7, 8, 12, 10, 14, 9, 13, 11, 15
		};

		public static TtyInfo Parse(byte[] buffer)
		{
			int intSize;


			int magic = GetInt16(buffer, 0);

			switch (magic)
			{
				case 0x11a:
					intSize = 2;
					break;

				case 0x21E:
					intSize = 4;
					break;

				default:
					// Unknown ttyinfo format
					return TtyInfo.Default;
			}

			int boolFieldLength = GetInt16(buffer, 4);
			int intFieldLength = GetInt16(buffer, 6);
			int strOffsetFieldLength = GetInt16(buffer, 8);

			// Normally i'd put a more complete implementation here, but I only need to parse this info to get the max color count
			// Feel free to implement the rest of this using these sources:
			// https://github.com/mono/mono/blob/master/mcs/class/corlib/System/TermInfoReader.cs
			// https://invisible-island.net/ncurses/man/term.5.html
			// https://invisible-island.net/ncurses/man/terminfo.5.html

			int baseOffset = 12 + GetString(buffer, 12).Length + 1; // Skip the terminal name
			baseOffset += boolFieldLength; // Length of bool field section
			baseOffset += baseOffset % 2; // Correct for boundary

			int colorOffset =
				baseOffset
				+ (intSize * (int)TermInfoNumbers.MaxColors); // Finally the offset for the max color integer

			//int stringOffset = baseOffset + (intSize * intFieldLength);

			//int foregoundColorOffset =
			//	stringOffset
			//	+ (2 * (int)TermInfoStrings.SetAForeground);

			//foregoundColorOffset = stringOffset
			//					   + (2 * strOffsetFieldLength)
			//					   + GetInt16(buffer, foregoundColorOffset);

			var info = new TtyInfo();

			info.MaxColors = GetInteger(intSize, buffer, colorOffset);

			//string setForegroundTemplate = GetString(buffer, foregoundColorOffset);

			//info.ForegroundColorStrings = ansiColorMapping.Select(x => setForegroundTemplate.Replace("%p1%", x.ToString())).ToArray();
			info.ForegroundColorStrings = ansiColorMapping.Select(x => $"\u001B[{(x > 7 ? 82 + x : 30 + x)}m").ToArray();

			return info;
		}

		private static int GetInt32(byte[] buffer, int offset)
		{
			return buffer[offset]
				   | (buffer[offset + 1] << 8)
				   | (buffer[offset + 2] << 16)
				   | (buffer[offset + 3] << 24);
		}

		private static short GetInt16(byte[] buffer, int offset)
		{
			return (short)(buffer[offset]
						   | (buffer[offset + 1] << 8));
		}

		private static int GetInteger(int intSize, byte[] buffer, int offset)
		{
			return intSize == 2
				? GetInt16(buffer, offset)
				: GetInt32(buffer, offset);
		}

		private static string GetString(byte[] buffer, int offset)
		{
			int length = 0;

			while (buffer[offset + length] != 0x00)
				length++;

			return Encoding.ASCII.GetString(buffer, offset, length);
		}

		internal enum TermInfoNumbers
		{
			MaxColors = 13
		}

		internal enum TermInfoStrings
		{
			SetAForeground = 359
		}
	}
}