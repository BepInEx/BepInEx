using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.Platforms;
using MonoMod.Utils;

namespace BepInEx.Preloader.RuntimeFixes;

internal static class XTermFix
{
    public static int intOffset;

    public static void Apply()
    {
        if (PlatformHelper.Is(Platform.Windows))
            return;

        if (typeof(Console).Assembly.GetType("System.ConsoleDriver") == null)
            // Mono version is too old, use our own TTY implementation instead
            return;

        if (AccessTools.Method("System.TermInfoReader:DetermineVersion") != null)
            // Fix has been applied officially
            return;

        // Apparently on older Unity versions (4.x), using Process.Start can run Console..cctor
        // And since MonoMod's PlatformHelper (used by DetourHelper.Native) runs Process.Start to determine ARM/x86 platform,
        // this causes a crash owing to TermInfoReader running before it can be patched and fixed
        // Because Doorstop does not support ARM at the moment, we can get away with just forcing x86 detour platform.
        // TODO: Figure out a way to detect ARM on Unix without running Process.Start
        DetourHelper.Native = new DetourNativeX86Platform();

        var harmony = new Harmony("com.bepinex.xtermfix");

        harmony.Patch(AccessTools.Method("System.TermInfoReader:ReadHeader"),
                      new HarmonyMethod(typeof(XTermFix), nameof(ReadHeaderPrefix)));

        harmony.Patch(AccessTools.Method("System.TermInfoReader:Get", new[] { AccessTools.TypeByName("System.TermInfoNumbers") }),
                      transpiler: new HarmonyMethod(typeof(XTermFix), nameof(GetTermInfoNumbersTranspiler)));

        harmony.Patch(AccessTools.Method("System.TermInfoReader:Get", new[] { AccessTools.TypeByName("System.TermInfoStrings") }),
                      transpiler: new HarmonyMethod(typeof(XTermFix), nameof(GetTermInfoStringsTranspiler)));

        harmony.Patch(AccessTools.Method("System.TermInfoReader:GetStringBytes", new[] { AccessTools.TypeByName("System.TermInfoStrings") }),
                      transpiler: new HarmonyMethod(typeof(XTermFix), nameof(GetTermInfoStringsTranspiler)));

        DetourHelper.Native = null;
    }

    public static int GetInt32(byte[] buffer, int offset)
    {
        int b1 = buffer[offset];
        int b2 = buffer[offset + 1];
        int b3 = buffer[offset + 2];
        int b4 = buffer[offset + 3];

        return b1 | (b2 << 8) | (b3 << 16) | (b4 << 24);
    }

    public static short GetInt16(byte[] buffer, int offset)
    {
        int b1 = buffer[offset];
        int b2 = buffer[offset + 1];

        return (short) (b1 | (b2 << 8));
    }

    public static int GetInteger(byte[] buffer, int offset) =>
        intOffset == 2
            ? GetInt16(buffer, offset)
            : GetInt32(buffer, offset);

    public static void DetermineVersion(short magic)
    {
        if (magic == 0x11a)
            intOffset = 2;
        else if (magic == 0x21e)
            intOffset = 4;
        else
            throw new Exception($"Unknown xterm header format: {magic}");
    }

    public static bool ReadHeaderPrefix(byte[] buffer,
                                        ref int position,
                                        ref short ___boolSize,
                                        ref short ___numSize,
                                        ref short ___strOffsets)
    {
        var magic = GetInt16(buffer, position);
        position += 2;
        DetermineVersion(magic);

        // nameSize = GetInt16(buffer, position);
        position += 2;
        ___boolSize = GetInt16(buffer, position);
        position += 2;
        ___numSize = GetInt16(buffer, position);
        position += 2;
        ___strOffsets = GetInt16(buffer, position);
        position += 2;
        // strSize = GetInt16(buffer, position);
        position += 2;

        return false;
    }

    public static IEnumerable<CodeInstruction> GetTermInfoNumbersTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        // This implementation does not seem to have changed so I will be using indexes like the lazy fuck I am

        var list = instructions.ToList();

        list[31] = new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(XTermFix), nameof(intOffset)));
        list[36] = new CodeInstruction(OpCodes.Nop);
        list[39] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(XTermFix), nameof(GetInteger)));

        return list;
    }

    public static IEnumerable<CodeInstruction> GetTermInfoStringsTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        // This implementation does not seem to have changed so I will be using indexes like the lazy fuck I am

        var list = instructions.ToList();

        list[32] = new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(XTermFix), nameof(intOffset)));

        return list;
    }
}
