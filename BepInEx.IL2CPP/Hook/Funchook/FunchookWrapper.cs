using System;
using System.Runtime.InteropServices;

namespace BepInEx.IL2CPP.Hook.Funchook;

internal enum FunchookResult : int
{
    InternalError = -1,
    Success = 0,
    OutOfMemory = 1,
    AlreadyInstalled = 2,
    Disassembly = 3,
    IPRelativeOffset = 4,
    CannotFixIPRelative = 5,
    FoundBackJump = 6,
    TooShortInstructions = 7,
    MemoryAllocation = 8,
    MemoryFunction = 9,
    NotInstalled = 10,
    NoAvailableRegisters = 11,
}

internal unsafe class FunchookWrapper
{
    internal static void SetDebugFile(string path) => FunchookImports.funchook_set_debug_file(path);

    public FunchookWrapper()
    {
        funchookInstance = FunchookImports.funchook_create();
    }

    public void Prepare(IntPtr* targetFunc, IntPtr detourFunc)
        => EnsureSuccess(FunchookImports.funchook_prepare(funchookInstance, targetFunc, detourFunc), nameof(FunchookImports.funchook_prepare));

    public void Install(int flags = 0)
        => EnsureSuccess(FunchookImports.funchook_install(funchookInstance, flags), nameof(FunchookImports.funchook_install));

    public void Uninstall(int flags = 0)
        => EnsureSuccess(FunchookImports.funchook_uninstall(funchookInstance, flags), nameof(FunchookImports.funchook_uninstall));

    public string GetErrorMessage()
        => FunchookImports.funchook_error_message(funchookInstance);

    public void Destroy()
        => EnsureSuccess(FunchookImports.funchook_destroy(funchookInstance), nameof(FunchookImports.funchook_destroy));

    private void EnsureSuccess(FunchookResult result, string methodName)
    {
        if (result == FunchookResult.Success) return;
        var errorMsg = GetErrorMessage();
        if (result == FunchookResult.OutOfMemory) throw new OutOfMemoryException($"{methodName} failed: {errorMsg}");
        throw new Exception($"{methodName} failed with result {result}: {errorMsg}");
    }

    private readonly IntPtr funchookInstance;
}
