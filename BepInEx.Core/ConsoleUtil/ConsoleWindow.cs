// --------------------------------------------------
// UnityInjector - ConsoleWindow.cs
// Copyright (c) Usagirei 2015 - 2015
// --------------------------------------------------

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace UnityInjector.ConsoleUtil
{
	internal class ConsoleWindow
	{
		public static bool IsAttached { get; private set; }
		private static IntPtr _cOut;
		private static IntPtr _oOut;

		public static TextWriter OriginalOut { get; set; }

		public static TextWriter StandardOut { get; private set; }

		public static void Attach()
		{
			if (IsAttached)
				return;

			if (_oOut == IntPtr.Zero)
				_oOut = GetStdHandle(-11);

			// Store Current Window
			IntPtr currWnd = GetForegroundWindow();

			//Check for existing console before allocating
			if (GetConsoleWindow() == IntPtr.Zero)
				if (!AllocConsole())
					throw new Exception("AllocConsole() failed");

			// Restore Foreground
			SetForegroundWindow(currWnd);

			_cOut = CreateFile("CONOUT$", 0x80000000 | 0x40000000, 2, IntPtr.Zero, 3, 0, IntPtr.Zero);
			BepInEx.ConsoleUtil.Kon.conOut = _cOut;

			if (!SetStdHandle(-11, _cOut))
				throw new Exception("SetStdHandle() failed");


			var originalOutStream = new FileStream(new SafeFileHandle(_oOut, false), FileAccess.Write);
			OriginalOut = new StreamWriter(originalOutStream, new UTF8Encoding(false));

			Init();

			IsAttached = true;
		}

		public static string Title
		{
			set
			{
				if (!IsAttached)
					return;

				if (value == null)
				{
					throw new ArgumentNullException(nameof(value));
				}

				if (value.Length > 24500)
				{
					throw new InvalidOperationException("Console title too long");
				}

				if (!SetConsoleTitle(value))
				{
					throw new InvalidOperationException("Console title invalid");
				}
			}
		}

		public static void Detach()
		{
			if (!IsAttached)
				return;

			if (!CloseHandle(_cOut))
				throw new Exception("CloseHandle() failed");
			_cOut = IntPtr.Zero;
			if (!FreeConsole())
				throw new Exception("FreeConsole() failed");
			if (!SetStdHandle(-11, _oOut))
				throw new Exception("SetStdHandle() failed");
			Init();

			IsAttached = false;
		}

		[DllImport("user32.dll")]
		private static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool SetForegroundWindow(IntPtr hWnd);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool AllocConsole();

		[DllImport("kernel32.dll")]
		private static extern IntPtr GetConsoleWindow();

		[DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
		private static extern bool CloseHandle(IntPtr handle);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr CreateFile(
			string fileName,
			uint desiredAccess,
			int shareMode,
			IntPtr securityAttributes,
			int creationDisposition,
			int flagsAndAttributes,
			IntPtr templateFile);

		[DllImport("kernel32.dll", SetLastError = false)]
		private static extern bool FreeConsole();

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr GetStdHandle(int nStdHandle);

		private static void Init()
		{
			var stdOut = Console.OpenStandardOutput();
			StandardOut = new StreamWriter(stdOut, Encoding.Default)
			{
				AutoFlush = true
			};

			Console.SetOut(StandardOut);
			Console.SetError(StandardOut);
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool SetStdHandle(int nStdHandle, IntPtr hConsoleOutput);

		[DllImport("kernel32.dll", BestFitMapping = true, CharSet = CharSet.Auto, SetLastError = true)]
		private static extern bool SetConsoleTitle(string title);
	}
}