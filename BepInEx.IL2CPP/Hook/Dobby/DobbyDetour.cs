using BepInEx.IL2CPP.Hook.Allocator;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Reflection;

namespace BepInEx.IL2CPP.Hook.Dobby;

internal class DobbyDetour : INativeDetour
{
    private static readonly ManualLogSource logger = Logger.CreateLogSource("DobbyDetour");
    public DobbyDetour(nint originalMethodPtr, nint detourMethodPtr)
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
    protected MethodInfo TrampolineMethod { get; set; }

    public void Apply()
    {
        if (IsApplied) return;

        PrepareDetour();
        DobbyImports.DobbyCommit(OriginalMethodPtr);

        logger.Log(LogLevel.Debug,
               $"Original: {OriginalMethodPtr:X}, Trampoline: {TrampolinePtr:X}, diff: {Math.Abs(OriginalMethodPtr - TrampolinePtr):X}; is within +-1GB range: {PageAllocator.IsInRelJmpRange(OriginalMethodPtr, TrampolinePtr)}");

        IsApplied = true;
    }
    public unsafe void Undo()
    {
        if (!IsApplied && !IsPrepared) return;

        if (NativeDetourHelper.DEBUG_DETOURS) logger.LogDebug($"Destroying detour");

        DobbyImports.DobbyDestroy(OriginalMethodPtr);
    }

    private unsafe void PrepareDetour()
    {
        if (IsPrepared) return;

        if (NativeDetourHelper.DEBUG_DETOURS) logger.LogDebug($"Preparing detour from 0x{OriginalMethodPtr:X2} to 0x{DetourMethodPtr:X2}");

        var trampolinePtr = IntPtr.Zero;
        DobbyImports.DobbyPrepare(OriginalMethodPtr, DetourMethodPtr, &trampolinePtr);

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

    public void Free() => IsValid = false;

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
}
