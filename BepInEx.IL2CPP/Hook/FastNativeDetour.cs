using System;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.IL2CPP.Hook.Allocator;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace BepInEx.IL2CPP.Hook;

[Obsolete("deprecated in favor of FunchookDetour", true)]
public class FastNativeDetour : IDetour
{
    private readonly FunchookDetour funchookDetour;
    public FastNativeDetour(IntPtr originalFunctionPtr, IntPtr detourFunctionPtr)
    {
        funchookDetour = new(originalFunctionPtr, detourFunctionPtr);
    }

    public bool IsValid => funchookDetour.IsValid;
    public bool IsApplied => funchookDetour.IsApplied;
    public void Apply() => funchookDetour.Apply();
    public void Dispose() => funchookDetour.Dispose();
    public void Free() => funchookDetour.Free();
    public MethodBase GenerateTrampoline(MethodBase signature = null) => funchookDetour.GenerateTrampoline(signature);
    public T GenerateTrampoline<T>() where T : Delegate => funchookDetour.GenerateTrampoline<T>();
    public void Undo() => funchookDetour.Undo();

    public static FastNativeDetour CreateAndApply<T>(IntPtr from,
                                                     T to,
                                                     out T original,
                                                     CallingConvention? callingConvention = null) where T : Delegate
    {
        var toPtr = callingConvention != null
                        ? MonoExtensions.GetFunctionPointerForDelegate(to, callingConvention.Value)
                        : Marshal.GetFunctionPointerForDelegate(to);
        var result = new FastNativeDetour(from, toPtr);
        original = result.GenerateTrampoline<T>();
        result.Apply();
        return result;
    }
}
