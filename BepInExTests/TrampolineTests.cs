using System;
using System.Runtime.InteropServices;
using BepInEx.IL2CPP;
using Iced.Intel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BepInEx.Tests;

[TestClass]
public class TrampolineTests
{
    [DataTestMethod]
    [DataRow(64)]
    [DataRow(32)]
    public void TrampolineTest(int bitness)
    {
        byte[] exampleCode =
        {
            0x48, 0x89, 0x5C, 0x24, 0x10, 0x48, 0x89, 0x74, 0x24, 0x18, 0x55, 0x57, 0x41, 0x56, 0x48, 0x8D,
            0xAC, 0x24, 0x00, 0xFF, 0xFF, 0xFF, 0x48, 0x81, 0xEC, 0x00, 0x02, 0x00, 0x00, 0x48, 0x8B, 0x05,
            0x18, 0x57, 0x0A, 0x00, 0x48, 0x33, 0xC4, 0x48, 0x89, 0x85, 0xF0, 0x00, 0x00, 0x00, 0x4C, 0x8B,
            0x05, 0x2F, 0x24, 0x0A, 0x00, 0x48, 0x8D, 0x05, 0x78, 0x7C, 0x04, 0x00, 0x33, 0xFF
        };

        var exampleCodePointer = Marshal.AllocHGlobal(80);
        var trampolineCodePointer = Marshal.AllocHGlobal(80);
        Marshal.Copy(exampleCode, 0, exampleCodePointer, exampleCode.Length);


        void Disassemble(byte[] data, ulong ip)
        {
            var formatter = new NasmFormatter();
            var output = new StringOutput();
            var codeReader = new ByteArrayCodeReader(data);
            var decoder = Decoder.Create(bitness, codeReader);
            decoder.IP = ip;
            while (codeReader.CanReadByte)
            {
                decoder.Decode(out var instr);
                formatter.Format(instr, output);
                Console.WriteLine($"{instr.IP:X16} {output.ToStringAndReset()}");
            }

            Console.WriteLine();
        }


        Console.WriteLine("Original:");
        Console.WriteLine();


        Disassemble(exampleCode, (ulong) exampleCodePointer.ToInt64());

        DetourGenerator.CreateTrampolineFromFunction(exampleCodePointer, out var trampolineLength, out _);

        Console.WriteLine("Modified:");
        Console.WriteLine();


        Marshal.Copy(exampleCodePointer, exampleCode, 0, exampleCode.Length);
        Disassemble(exampleCode, (ulong) exampleCodePointer.ToInt64());


        Console.WriteLine();
        Console.WriteLine("Trampoline:");
        Console.WriteLine();

        var trampolineArray = new byte[trampolineLength];
        Marshal.Copy(trampolineCodePointer, trampolineArray, 0, trampolineLength);

        Disassemble(trampolineArray, (ulong) trampolineCodePointer.ToInt64());


        Marshal.FreeHGlobal(exampleCodePointer);
        Marshal.FreeHGlobal(trampolineCodePointer);

        Assert.IsFalse(false);
    }
}
