using BepInEx.IL2CPP.Hook.Allocator;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BepInEx.IL2CPP.Hook.Funchook;

internal class FunchookDetour : INativeDetour
{
    private static readonly ManualLogSource logger = Logger.CreateLogSource("FunchookDetour");
    public FunchookDetour(nint originalMethodPtr, nint detourMethodPtr)
    {
        OriginalMethodPtr = originalMethodPtr;
        DetourMethodPtr = detourMethodPtr;
    }

    public nint OriginalMethodPtr { get; protected set; }
    public nint DetourMethodPtr { get; protected set; }
    public nint TrampolinePtr { get; protected set; }

    public bool IsValid { get; protected set; } = true;

    public bool IsApplied { get; protected set; }
    public bool IsPrepared { get; protected set; }

    internal FunchookWrapper FunchookInstance { get; set; }
    protected MethodInfo TrampolineMethod { get; set; }

    public void Apply()
    {
        if (IsApplied) return;

        PrepareDetour();
        if (NativeDetourHelper.DEBUG_DETOURS) logger.LogDebug($"Installing funchook instance");
        FunchookInstance.Install();

        logger.Log(LogLevel.Debug,
               $"Original: {OriginalMethodPtr:X}, Trampoline: {TrampolinePtr:X}, diff: {Math.Abs(OriginalMethodPtr - TrampolinePtr):X}; is within +-1GB range: {PageAllocator.IsInRelJmpRange(OriginalMethodPtr, TrampolinePtr)}");

        IsApplied = true;
    }
    public void Undo()
    {
        if (IsApplied && IsPrepared)
        {
            if (NativeDetourHelper.DEBUG_DETOURS) logger.LogDebug($"Uninstalling funchook instance");
            FunchookInstance.Uninstall();
        }
    }

    private unsafe void PrepareDetour()
    {
        if (IsPrepared) return;

        if (NativeDetourHelper.DEBUG_DETOURS) logger.LogDebug($"Creating funchook instance");

        if (FunchookInstance == null)
            FunchookInstance = new FunchookWrapper();

        if (NativeDetourHelper.DEBUG_DETOURS) logger.LogDebug($"Preparing detour from 0x{OriginalMethodPtr:X2} to 0x{DetourMethodPtr:X2}");

        var trampolinePtr = OriginalMethodPtr;
        FunchookInstance.Prepare(&trampolinePtr, DetourMethodPtr);

        if (NativeDetourHelper.DEBUG_DETOURS) logger.LogDebug($"Prepared detour; Trampoline: 0x{trampolinePtr:X2}");

        TrampolinePtr = trampolinePtr;
        IsPrepared = true;
    }

    public void Dispose()
    {
        if (!IsValid) return;
        Undo();
        Free();
    }

    public void Free()
    {
        if (NativeDetourHelper.DEBUG_DETOURS) logger.LogDebug($"Destroying funchook instance");
        FunchookInstance.Destroy();
        IsValid = false;
    }

    public MethodBase GenerateTrampoline(MethodBase signature = null)
    {
        if (TrampolineMethod == null)
        {
            PrepareDetour();
            TrampolineMethod = DetourHelper.GenerateNativeProxy(TrampolinePtr, signature);
        }
        return TrampolineMethod;
    }
    public T GenerateTrampoline<T>() where T : Delegate
    {
        if (!typeof(Delegate).IsAssignableFrom(typeof(T)))
            throw new InvalidOperationException($"Type {typeof(T)} not a delegate type.");

        return GenerateTrampoline(typeof(T).GetMethod("Invoke")).CreateDelegate(typeof(T)) as T;
    }

    public static FunchookDetour CreateAndApply<T>(IntPtr from,
                                                     T to,
                                                     out T original,
                                                     CallingConvention? callingConvention = null) where T : Delegate
    {
        var toPtr = callingConvention != null
                        ? MonoExtensions.GetFunctionPointerForDelegate(to, callingConvention.Value)
                        : Marshal.GetFunctionPointerForDelegate(to);
        var result = new FunchookDetour(from, toPtr);
        original = result.GenerateTrampoline<T>();
        result.Apply();
        return result;
    }
}
