using System;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.IL2CPP.Hook.Allocator;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace BepInEx.IL2CPP.Hook;

public class FastNativeDetour : IDetour
{
    private static readonly ManualLogSource logger = Logger.CreateLogSource("FastNativeDetour");


    public FastNativeDetour(IntPtr originalFunctionPtr, IntPtr detourFunctionPtr)
    {
        OriginalFunctionPtr = originalFunctionPtr;
        DetourFunctionPtr = detourFunctionPtr;

        // TODO: This may not be safe during undo if the method is smaller than 20 bytes
        BackupBytes = new byte[20];
        Marshal.Copy(originalFunctionPtr, BackupBytes, 0, 20);
    }

    protected byte[] BackupBytes { get; set; }


    public IntPtr OriginalFunctionPtr { get; protected set; }
    public IntPtr DetourFunctionPtr { get; protected set; }


    public IntPtr TrampolinePtr { get; protected set; } = IntPtr.Zero;
    public int TrampolineSize { get; protected set; }
    protected int TrampolineJmpSize { get; set; }

    protected MethodInfo TrampolineMethod { get; set; }

    public bool IsValid { get; protected set; } = true;
    public bool IsApplied { get; protected set; }

    public void Apply() => Apply(null);


    public void Undo()
    {
        if (!IsApplied)
            return;

        Marshal.Copy(BackupBytes, 0, OriginalFunctionPtr, BackupBytes.Length);

        PageAllocator.Instance.Free(TrampolinePtr);

        TrampolinePtr = IntPtr.Zero;
        TrampolineSize = 0;

        IsApplied = false;
    }

    public void Free() => IsValid = false;

    public MethodBase GenerateTrampoline(MethodBase signature = null)
    {
        if (TrampolineMethod == null)
        {
            // Generate trampoline without applying the detour
            GenerateTrampolineInner(out _, out _);

            if (TrampolinePtr == IntPtr.Zero)
                throw new InvalidOperationException("Trampoline pointer is not available");

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

    public void Dispose()
    {
        if (!IsValid)
            return;

        Undo();
        Free();
    }

    public void Apply(ManualLogSource debuggerLogSource)
    {
        if (IsApplied)
            return;


        DetourHelper.Native.MakeWritable(OriginalFunctionPtr, 32);

        if (debuggerLogSource != null)
        {
            debuggerLogSource
                .LogDebug($"Detouring 0x{OriginalFunctionPtr.ToString("X")} -> 0x{DetourFunctionPtr.ToString("X")}");
            debuggerLogSource.Log(LogLevel.Debug, "Original (32) asm");
            DetourGenerator.Disassemble(debuggerLogSource, OriginalFunctionPtr, 32);
        }

        var arch = IntPtr.Size == 8 ? Architecture.X64 : Architecture.X86;

        GenerateTrampolineInner(out var trampolineLength, out var jmpLength);

        DetourGenerator.ApplyDetour(OriginalFunctionPtr, DetourFunctionPtr, arch, trampolineLength - jmpLength);

        if (debuggerLogSource != null)
        {
            debuggerLogSource.Log(LogLevel.Debug, $"Trampoline allocation: 0x{TrampolinePtr.ToString("X")}");
            debuggerLogSource.Log(LogLevel.Debug, "Modified (32) asm");
            DetourGenerator.Disassemble(debuggerLogSource, OriginalFunctionPtr, 32);
            debuggerLogSource.Log(LogLevel.Debug, $"Trampoline ({trampolineLength}) asm");
            DetourGenerator.Disassemble(debuggerLogSource, TrampolinePtr, trampolineLength);
        }

        DetourHelper.Native.MakeExecutable(OriginalFunctionPtr, 32);

        IsApplied = true;
    }


    private void GenerateTrampolineInner(out int trampolineLength, out int jmpLength)
    {
        if (TrampolinePtr != IntPtr.Zero)
        {
            trampolineLength = TrampolineSize;
            jmpLength = TrampolineJmpSize;
            return;
        }

        var instructionBuffer = new byte[32];
        Marshal.Copy(OriginalFunctionPtr, instructionBuffer, 0, 32);

        var trampolineAlloc = PageAllocator.Instance.Allocate(OriginalFunctionPtr);

        logger.Log(LogLevel.Debug,
                   $"Original: {OriginalFunctionPtr.ToInt64():X}, Trampoline: {trampolineAlloc:X}, diff: {Math.Abs(OriginalFunctionPtr.ToInt64() - trampolineAlloc):X}; is within +-1GB range: {PageAllocator.IsInRelJmpRange(OriginalFunctionPtr, trampolineAlloc)}");

        DetourHelper.Native.MakeWritable(trampolineAlloc, PageAllocator.PAGE_SIZE);

        var arch = IntPtr.Size == 8 ? Architecture.X64 : Architecture.X86;

        DetourGenerator.CreateTrampolineFromFunction(instructionBuffer, OriginalFunctionPtr, trampolineAlloc,
                                                     DetourGenerator.GetDetourLength(arch), arch,
                                                     out trampolineLength, out jmpLength);

        DetourHelper.Native.MakeExecutable(trampolineAlloc, PageAllocator.PAGE_SIZE);

        TrampolinePtr = trampolineAlloc;
        TrampolineSize = trampolineLength;
        TrampolineJmpSize = jmpLength;
    }

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
