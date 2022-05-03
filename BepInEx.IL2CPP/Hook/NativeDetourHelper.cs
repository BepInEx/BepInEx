using BepInEx.IL2CPP.Hook.Funchook;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BepInEx.IL2CPP.Hook;

public class NativeDetourHelper
{
    public static bool DEBUG_DETOURS = false;
    public static INativeDetour Create(nint original, nint target)
    {
        return new FunchookDetour(original, target);
    }

    public static INativeDetour CreateAndApply<T>(nint from, T to, out T original, CallingConvention? callingConvention = null)
        where T : Delegate
    {
        var toPtr = callingConvention != null
            ? MonoExtensions.GetFunctionPointerForDelegate(to, callingConvention.Value)
            : Marshal.GetFunctionPointerForDelegate(to);

        var detour = Create(from, toPtr);
        original = detour.GenerateTrampoline<T>();
        detour.Apply();
        return detour;
    }
}
