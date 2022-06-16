using System;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;

namespace BepInEx.IL2CPP.Hook;

internal abstract class BaseNativeDetour<T> : INativeDetour where T : BaseNativeDetour<T>
{
    protected static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource(typeof(T).Name);

    protected BaseNativeDetour(nint originalMethodPtr, Delegate detourMethod)
    {
        OriginalMethodPtr = originalMethodPtr;
        DetourMethod = detourMethod;
        DetourMethodPtr = Marshal.GetFunctionPointerForDelegate(detourMethod);
    }

    public bool IsPrepared { get; protected set; }
    protected MethodInfo TrampolineMethod { get; set; }
    protected Delegate DetourMethod { get; set; }

    public nint OriginalMethodPtr { get; }
    public nint DetourMethodPtr { get; }
    public nint TrampolinePtr { get; protected set; }
    public bool IsValid { get; private set; } = true;
    public bool IsApplied { get; private set; }

    public void Dispose()
    {
        if (!IsValid) return;
        Undo();
        Free();
    }

    public void Apply()
    {
        if (IsApplied) return;

        Prepare();
        ApplyImpl();

        Logger.Log(LogLevel.Debug,
                   $"Original: {OriginalMethodPtr:X}, Trampoline: {TrampolinePtr:X}, diff: {Math.Abs(OriginalMethodPtr - TrampolinePtr):X}");

        IsApplied = true;
    }

    public void Undo()
    {
        if (IsApplied && IsPrepared) UndoImpl();
    }

    public void Free()
    {
        FreeImpl();
        IsValid = false;
    }

    public MethodBase GenerateTrampoline(MethodBase signature = null)
    {
        if (TrampolineMethod == null)
        {
            Prepare();
            TrampolineMethod = DetourHelper.GenerateNativeProxy(TrampolinePtr, signature);
        }

        return TrampolineMethod;
    }

    public TDelegate GenerateTrampoline<TDelegate>() where TDelegate : Delegate
    {
        if (!typeof(Delegate).IsAssignableFrom(typeof(TDelegate)))
            throw new InvalidOperationException($"Type {typeof(TDelegate)} not a delegate type.");

        _ = GenerateTrampoline(typeof(TDelegate).GetMethod("Invoke"));

        return Marshal.GetDelegateForFunctionPointer<TDelegate>(TrampolinePtr);
    }

    protected abstract void ApplyImpl();

    private void Prepare()
    {
        if (IsPrepared) return;
        Logger.LogDebug($"Preparing detour from 0x{OriginalMethodPtr:X2} to 0x{DetourMethodPtr:X2}");
        PrepareImpl();
        Logger.LogDebug($"Prepared detour; Trampoline: 0x{TrampolinePtr:X2}");
        IsPrepared = true;
    }

    protected abstract void PrepareImpl();

    protected abstract void UndoImpl();

    protected abstract void FreeImpl();
}
