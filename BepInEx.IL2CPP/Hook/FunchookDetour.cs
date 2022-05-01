using BepInEx.IL2CPP.Hook.Allocator;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BepInEx.IL2CPP.Hook;

public class FunchookDetour : IDetour
{
    public static bool DEBUG_DETOURS = false;

    private static readonly ManualLogSource logger = Logger.CreateLogSource("FunchookDetour");
    public FunchookDetour(IntPtr originalFunctionPtr, IntPtr detourFunctionPtr)
    {
        OriginalFunctionPtr = originalFunctionPtr;
        DetourFunctionPtr = detourFunctionPtr;
    }

    public IntPtr OriginalFunctionPtr { get; protected set; }
    public IntPtr DetourFunctionPtr { get; protected set; }
    public IntPtr TrampolinePtr { get; protected set; }

    public bool IsValid { get; protected set; } = true;

    public bool IsApplied { get; protected set; }
    public bool IsPrepared { get; protected set; }

    internal FunchookWrapper FunchookInstance { get; set; }
    protected MethodInfo TrampolineMethod { get; set; }

    public void Apply()
    {
        if (IsApplied) return;

        PrepareDetour();
        if (DEBUG_DETOURS) logger.LogDebug($"Installing funchook instance");
        FunchookInstance.Install();

        logger.Log(LogLevel.Debug,
               $"Original: {OriginalFunctionPtr.ToInt64():X}, Trampoline: {TrampolinePtr:X}, diff: {Math.Abs(OriginalFunctionPtr.ToInt64() - TrampolinePtr.ToInt64()):X}; is within +-1GB range: {PageAllocator.IsInRelJmpRange(OriginalFunctionPtr, TrampolinePtr)}");

        IsApplied = true;
    }
    public void Undo()
    {
        if (IsApplied && IsPrepared)
        {
            if (DEBUG_DETOURS) logger.LogDebug($"Uninstalling funchook instance");
            FunchookInstance.Uninstall();
        }
    }

    private unsafe void PrepareDetour()
    {
        if (IsPrepared) return;

        if (DEBUG_DETOURS) logger.LogDebug($"Creating funchook instance");

        if (FunchookInstance == null)
            FunchookInstance = new FunchookWrapper();

        if (DEBUG_DETOURS) logger.LogDebug($"Preparing detour from 0x{OriginalFunctionPtr.ToInt64():X2} to 0x{DetourFunctionPtr.ToInt64():X2}");

        var trampolinePtr = OriginalFunctionPtr;
        FunchookInstance.Prepare(&trampolinePtr, DetourFunctionPtr);

        if (DEBUG_DETOURS) logger.LogDebug($"Prepared detour; Trampoline: 0x{trampolinePtr.ToInt64():X2}");

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
        if (DEBUG_DETOURS) logger.LogDebug($"Destroying funchook instance");
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
