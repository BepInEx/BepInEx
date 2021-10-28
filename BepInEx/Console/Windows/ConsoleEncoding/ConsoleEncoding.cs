// --------------------------------------------------
// UnityInjector - ConsoleEncoding.cs
// Copyright (c) Usagirei 2015 - 2015
// --------------------------------------------------

using System;
using System.Text;

namespace UnityInjector.ConsoleUtil
{
	// --------------------------------------------------
	// Code ported from
	// https://gist.github.com/asm256/9bfb88336a1433e2328a
	// Which in turn was seemingly ported from
	// http://jonskeet.uk/csharp/ebcdic/
	// using only safe (managed) code
	// --------------------------------------------------
	internal partial class ConsoleEncoding : Encoding
	{
		private readonly uint _codePage;
		public override int CodePage => (int)_codePage;

		public static Encoding OutputEncoding => new ConsoleEncoding(ConsoleCodePage);

		public static uint ConsoleCodePage
		{
			get { return GetConsoleOutputCP(); }
			set { SetConsoleOutputCP(value); }
		}

		public static uint GetActiveCodePage()
		{
			return GetACP();
		}

		private ConsoleEncoding(uint codePage)
		{
			_codePage = codePage;
		}

		public static ConsoleEncoding GetEncoding(uint codePage)
		{
			return new ConsoleEncoding(codePage);
		}

		public override int GetByteCount(char[] chars, int index, int count)
		{
			WriteCharBuffer(chars, index, count);
			int result = WideCharToMultiByte(_codePage, 0, _charBuffer, count, _zeroByte, 0, IntPtr.Zero, IntPtr.Zero);
			return result;
		}

		public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
		{
			var byteCount = GetByteCount(chars, charIndex, charCount);

			WriteCharBuffer(chars, charIndex, charCount);

			ExpandByteBuffer(byteCount);
			int result = WideCharToMultiByte(_codePage, 0, chars, charCount, _byteBuffer, byteCount, IntPtr.Zero, IntPtr.Zero);
			ReadByteBuffer(bytes, byteIndex, byteCount);

			return result;
		}

		public override int GetCharCount(byte[] bytes, int index, int count)
		{
			WriteByteBuffer(bytes, index, count);
			int result = MultiByteToWideChar(_codePage, 0, bytes, count, _zeroChar, 0);
			return result;
		}

		public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
		{
			var charCount = GetCharCount(bytes, byteIndex, byteCount);

			WriteByteBuffer(bytes, byteIndex, byteCount);

			ExpandCharBuffer(charCount);
			int result = MultiByteToWideChar(_codePage, 0, bytes, byteCount, _charBuffer, charCount);
			ReadCharBuffer(chars, charIndex, charCount);

			return result;
		}

		public override int GetMaxByteCount(int charCount) => charCount * 2;
		public override int GetMaxCharCount(int byteCount) => byteCount;
	}
}