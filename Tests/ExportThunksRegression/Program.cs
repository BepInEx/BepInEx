using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;

namespace ExportThunksRegression;

/// <summary>
/// Regression coverage for <c>INativeDetour.FollowExportThunks</c>.
///
/// The real production method (private static, reached by reflection) is invoked against synthetic
/// stubs allocated with <see cref="Marshal.AllocHGlobal(int)" />. No machine code is executed, no
/// detour provider (Dobby/Funchook) is constructed, and no real game or BepInEx directory is
/// touched: BepInEx paths are redirected into a throwaway temp directory before the production
/// type is first used.
/// </summary>
internal static class Program
{
    private const int BufferSize = 0x100;

    // Whether the process can legitimately contain the x86/x64 stub encodings decoded by
    // FollowExportThunks. On every other architecture the method must return the pointer unchanged.
    private static readonly bool FollowsThunks =
        RuntimeInformation.ProcessArchitecture is Architecture.X86 or Architecture.X64;

    private static MethodInfo followExportThunks;
    private static int failures;

    private static int Main()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "bepinex-export-thunks-" + Guid.NewGuid().ToString("N"));
        try
        {
            IsolateBepInExPaths(tempRoot);

            followExportThunks = typeof(BepInEx.Unity.IL2CPP.Hook.INativeDetour)
                                 .GetMethod("FollowExportThunks", BindingFlags.NonPublic | BindingFlags.Static);
            if (followExportThunks == null ||
                !followExportThunks.IsStatic ||
                followExportThunks.ReturnType != typeof(nint) ||
                followExportThunks.GetParameters().Length != 1 ||
                followExportThunks.GetParameters()[0].ParameterType != typeof(nint))
            {
                Console.Error.WriteLine("FATAL: INativeDetour.FollowExportThunks(nint) not found; cannot run the regression.");
                return 2;
            }

            Console.WriteLine($"ProcessArchitecture: {RuntimeInformation.ProcessArchitecture}");
            Console.WriteLine($"IntPtr.Size: {IntPtr.Size}");
            Console.WriteLine(FollowsThunks
                                  ? "Expectation: x86/x64 - stubs are followed to their targets"
                                  : "Expectation: not x86/x64 - the original pointer is returned unchanged");
            Console.WriteLine();

            RunNullTest();
            RunOrdinaryBodyTest();
            RunForwardRel32Test();
            RunBackwardRel32Test();
            RunRipRelativeIndirectTest();
            RunAbsolute32IndirectTest();
            RunMixedChainTest();
            RunHopBudgetTest();
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }

        Console.WriteLine();
        if (failures == 0)
        {
            Console.WriteLine("All checks passed.");
            return 0;
        }

        Console.Error.WriteLine($"{failures} check(s) failed.");
        return 1;
    }

    private static void RunNullTest() =>
        Check("null pointer stays null", () => Follow(0), 0);

    private static void RunOrdinaryBodyTest() =>
        WithBuffer(buffer =>
        {
            Write(buffer, 0x55, 0x48, 0x89, 0xE5, 0xC3); // push rbp; mov rbp, rsp; ret
            Check("ordinary function body is not followed", () => Follow(buffer), buffer);
        });

    private static void RunForwardRel32Test() =>
        WithBuffer(buffer =>
        {
            var target = buffer + 0x40;
            WriteJumpRel32(buffer, target);
            Write(target, 0x55, 0xC3); // body inside the buffer keeps unguarded decoding readable
            Check("E9 rel32 forward jump", () => Follow(buffer), FollowsThunks ? target : buffer);
        });

    private static void RunBackwardRel32Test() =>
        WithBuffer(buffer =>
        {
            var thunk = buffer + 0x40;
            WriteJumpRel32(thunk, buffer);
            Write(buffer, 0x55, 0xC3);
            Check("E9 rel32 backward jump", () => Follow(thunk), FollowsThunks ? buffer : thunk);
        });

    private static void RunRipRelativeIndirectTest()
    {
        if (IntPtr.Size != 8)
        {
            Skip("FF 25 RIP-relative indirect jump", "needs a 64-bit process");
            return;
        }

        WithBuffer(buffer =>
        {
            var target = buffer + 0x40;
            var slot = buffer + 0x80;
            WriteJmpRipRelative(buffer, slot);
            Marshal.WriteIntPtr(slot, target);
            Write(target, 0x55, 0xC3);
            Check("FF 25 RIP-relative indirect jump", () => Follow(buffer), FollowsThunks ? target : buffer);
        });
    }

    private static void RunAbsolute32IndirectTest()
    {
        // FF 25 with an absolute 32-bit operand only exists in a real 32-bit x86 process; it is
        // never simulated from a 64-bit process (the operand could not be encoded honestly).
        if (IntPtr.Size != 4 || RuntimeInformation.ProcessArchitecture != Architecture.X86)
        {
            Skip("FF 25 absolute32 indirect jump", "needs a real 32-bit x86 process");
            return;
        }

        WithBuffer(buffer =>
        {
            var target = buffer + 0x40;
            var slot = buffer + 0x80;
            WriteJmpAbsolute32(buffer, slot);
            Marshal.WriteIntPtr(slot, target);
            Write(target, 0x55, 0xC3);
            Check("FF 25 absolute32 indirect jump", () => Follow(buffer), target);
        });
    }

    private static void RunMixedChainTest() =>
        WithBuffer(buffer =>
        {
            var first = buffer + 0x00;  // E9 -> second
            var second = buffer + 0x20; // FF 25 -> slot -> third
            var third = buffer + 0x60;  // E9 -> body
            var body = buffer + 0x80;
            var slot = buffer + 0xF8;

            WriteJumpRel32(first, second);
            if (IntPtr.Size == 8)
                WriteJmpRipRelative(second, slot);
            else
                WriteJmpAbsolute32(second, slot);
            Marshal.WriteIntPtr(slot, third);
            WriteJumpRel32(third, body);
            Write(body, 0x55, 0xC3);

            Check("mixed E9/FF 25 chain", () => Follow(first), FollowsThunks ? body : first);
        });

    private static void RunHopBudgetTest() =>
        WithBuffer(buffer =>
        {
            const int stride = 8;
            const int thunkCount = 9; // one more than the 8-hop budget

            for (var i = 0; i < thunkCount; i++)
                WriteJumpRel32(buffer + i * stride, buffer + (i + 1) * stride);
            Write(buffer + thunkCount * stride, 0x55, 0xC3);

            // At most 8 jumps are performed, so the still-jumping 9th thunk is returned undecoded.
            Check("hop budget stops after 8 jumps", () => Follow(buffer), FollowsThunks ? buffer + 8 * stride : buffer);
        });

    private static nint Follow(nint func) => (nint)followExportThunks.Invoke(null, new object[] { func });

    private static void WithBuffer(Action<nint> test)
    {
        var buffer = Marshal.AllocHGlobal(BufferSize);
        try { test(buffer); }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static void Check(string name, Func<nint> run, nint expected)
    {
        nint actual;
        try
        {
            actual = run();
        }
        catch (Exception e)
        {
            var cause = e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e;
            failures++;
            Console.WriteLine($"FAIL {name}: threw {cause.GetType().Name}: {cause.Message}");
            return;
        }

        if (actual == expected)
        {
            Console.WriteLine($"PASS {name}");
        }
        else
        {
            failures++;
            Console.WriteLine($"FAIL {name}: expected 0x{(long)expected:X}, got 0x{(long)actual:X}");
        }
    }

    private static void Skip(string name, string reason) => Console.WriteLine($"SKIP {name}: {reason}");

    private static void Write(nint address, params byte[] bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
            Marshal.WriteByte(address, i, bytes[i]);
    }

    private static void WriteJumpRel32(nint thunk, nint target)
    {
        Marshal.WriteByte(thunk, 0xE9);
        Marshal.WriteInt32(thunk + 1, checked((int)(target - (thunk + 5))));
    }

    private static void WriteJmpRipRelative(nint thunk, nint slot)
    {
        // FF /4 with modrm 0x25: jmp qword ptr [rip + disp32] (x64).
        Marshal.WriteByte(thunk, 0xFF);
        Marshal.WriteByte(thunk + 1, 0x25);
        Marshal.WriteInt32(thunk + 2, checked((int)(slot - (thunk + 6))));
    }

    private static void WriteJmpAbsolute32(nint thunk, nint slot)
    {
        // FF /4 with modrm 0x25: jmp dword ptr [disp32] (absolute address, 32-bit processes only).
        Marshal.WriteByte(thunk, 0xFF);
        Marshal.WriteByte(thunk + 1, 0x25);
        Marshal.WriteInt32(thunk + 2, unchecked((int)slot));
    }

    private static void IsolateBepInExPaths(string tempRoot)
    {
        // Touching the production type runs INativeDetour's static initializer, which binds
        // ConfigFile.CoreConfig and can write BepInEx.cfg. Point every BepInEx path at the
        // throwaway root so no real game or BepInEx install is read or written. GameRootPath
        // derivation is bypassed by supplying managedPath/gameDataRelativeToManaged explicitly.
        var fakeExecutable = Path.Combine(tempRoot, "FakeGame.app", "Contents", "MacOS", "FakeGame");
        var fakeManagedDirectory = Path.Combine(tempRoot, "FakeGame_Data", "Managed");
        var fakeBepInExRoot = Path.Combine(tempRoot, "BepInEx");

        Directory.CreateDirectory(Path.GetDirectoryName(fakeExecutable));
        Directory.CreateDirectory(fakeManagedDirectory);
        Directory.CreateDirectory(Path.Combine(fakeBepInExRoot, "config"));
        File.WriteAllBytes(fakeExecutable, Array.Empty<byte>());

        Paths.SetExecutablePath(fakeExecutable,
                                bepinRootPath: fakeBepInExRoot,
                                managedPath: fakeManagedDirectory,
                                gameDataRelativeToManaged: true);
    }

    private static void TryDeleteDirectory(string path)
    {
        try { Directory.Delete(path, true); }
        catch { /* best effort; the directory lives under the OS temp folder */ }
    }
}
