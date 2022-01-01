// --------------------------------------------------
// UnityInjector - ConsoleEncoding.cs
// Copyright (c) Usagirei 2015 - 2015
// --------------------------------------------------

using System;
using System.Text;

namespace UnityInjector.ConsoleUtil;

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

    private ConsoleEncoding(uint codePage)
    {
        _codePage = codePage;
    }

    public override int CodePage => (int)_codePage;

    public static Encoding OutputEncoding => new ConsoleEncoding(ConsoleCodePage);

    public static uint ConsoleCodePage
    {
        get => GetConsoleOutputCP();
        set => SetConsoleOutputCP(value);
    }

    public static uint GetActiveCodePage() => GetACP();

    public static ConsoleEncoding GetEncoding(uint codePage) => new(codePage);

    public override int GetByteCount(char[] chars, int index, int count)
    {
        WriteCharBuffer(chars, index, count);
        var result = WideCharToMultiByte(_codePage, 0, chars, count, _zeroByte, 0, IntPtr.Zero, IntPtr.Zero);
        return result;
    }

    public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
    {
        var byteCount = GetByteCount(chars, charIndex, charCount);
        WriteCharBuffer(chars, charIndex, charCount);
        ExpandByteBuffer(byteCount);
        _ = WideCharToMultiByte(_codePage, 0, chars, charCount, _byteBuffer, byteCount, IntPtr.Zero,
                                IntPtr.Zero);
        var readCount = Math.Min(bytes.Length, byteCount);
        ReadByteBuffer(bytes, byteIndex, readCount);
        return readCount;
    }

    public override int GetCharCount(byte[] bytes, int index, int count)
    {
        WriteByteBuffer(bytes, index, count);
        var result = MultiByteToWideChar(_codePage, 0, bytes, count, _zeroChar, 0);
        return result;
    }

    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
    {
        var charCount = GetCharCount(bytes, byteIndex, byteCount);
        WriteByteBuffer(bytes, byteIndex, byteCount);
        ExpandCharBuffer(charCount);
        _ = MultiByteToWideChar(_codePage, 0, bytes, byteCount, _charBuffer, charCount);
        var readCount = Math.Min(chars.Length, charCount);
        ReadCharBuffer(chars, charIndex, readCount);
        return readCount;
    }

    // Account for some exotic UTF-8 characters
    public override int GetMaxByteCount(int charCount) => charCount * 4;
    public override int GetMaxCharCount(int byteCount) => byteCount;
}
