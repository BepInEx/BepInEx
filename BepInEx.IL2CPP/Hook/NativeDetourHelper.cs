using BepInEx.IL2CPP.Hook.Dobby;
using BepInEx.IL2CPP.Hook.Funchook;
using System;
using System.Runtime.InteropServices;

namespace BepInEx.IL2CPP.Hook;

public class NativeDetourHelper
{
    public static bool DEBUG_DETOURS = false;
    public static INativeDetour Create(nint original, nint target)
    {
        return new DobbyDetour(original, target);
        //return new FunchookDetour(original, target);
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
