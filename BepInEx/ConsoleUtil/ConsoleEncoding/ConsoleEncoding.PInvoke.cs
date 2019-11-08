// --------------------------------------------------
// UnityInjector - ConsoleEncoding.PInvoke.cs
// Copyright (c) Usagirei 2015 - 2015
// --------------------------------------------------

using System;
using System.Runtime.InteropServices;

namespace UnityInjector.ConsoleUtil
{
	// --------------------------------------------------
	// Code ported from
	// https://gist.github.com/asm256/9bfb88336a1433e2328a
	// Which in turn was seemingly ported from
	// http://jonskeet.uk/csharp/ebcdic/
	// using only safe (managed) code
	// --------------------------------------------------
	internal partial class ConsoleEncoding
	{
		[DllImport("kernel32.dll")]
		private static extern uint GetConsoleOutputCP();

		[DllImport("kernel32.dll")]
		private static extern uint GetACP();

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern int MultiByteToWideChar(
			uint codePage,
			uint dwFlags,
			[In, MarshalAs(UnmanagedType.LPArray)] byte[] lpMultiByteStr,
			int cbMultiByte,
			[Out, MarshalAs(UnmanagedType.LPWStr)]
			char[] lpWideCharStr,
			int cchWideChar);

		[DllImport("kernel32.dll")]
		private static extern IntPtr SetConsoleOutputCP(uint codepage);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern int WideCharToMultiByte(
			uint codePage,
			uint dwFlags,
			[In, MarshalAs(UnmanagedType.LPWStr)] char[] lpWideCharStr,
			int cchWideChar,
			[Out, MarshalAs(UnmanagedType.LPArray)]
			byte[] lpMultiByteStr,
			int cbMultiByte,
			IntPtr lpDefaultChar,
			IntPtr lpUsedDefaultChar);
	}
}