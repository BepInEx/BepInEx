using System;
using System.Runtime.InteropServices;

namespace BepInEx.IL2CPP.Hook;

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
    internal static void SetDebugFile(string path) => funchook_set_debug_file(path);

    public FunchookWrapper()
    {
        funchookInstance = funchook_create();
    }

    public void Prepare(IntPtr* targetFunc, IntPtr detourFunc)
        => EnsureSuccess(funchook_prepare(funchookInstance, targetFunc, detourFunc), nameof(funchook_prepare));

    public void Install(int flags = 0)
        => EnsureSuccess(funchook_install(funchookInstance, flags), nameof(funchook_install));

    public void Uninstall(int flags = 0)
        => EnsureSuccess(funchook_uninstall(funchookInstance, flags), nameof(funchook_uninstall));

    public string GetErrorMessage()
        => funchook_error_message(funchookInstance);

    public void Destroy()
        => EnsureSuccess(funchook_destroy(funchookInstance), nameof(funchook_destroy));

    private void EnsureSuccess(FunchookResult result, string methodName)
    {
        if (result == FunchookResult.Success) return;
        var errorMsg = GetErrorMessage();
        if (result == FunchookResult.OutOfMemory) throw new OutOfMemoryException($"{methodName} failed: {errorMsg}");
        throw new Exception($"{methodName} failed with result {result}: {errorMsg}");
    }

    private readonly IntPtr funchookInstance;

    #region Imports
    [DllImport("funchook", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr funchook_create();
    [DllImport("funchook", CallingConvention = CallingConvention.Cdecl)]
    private static extern FunchookResult funchook_destroy(IntPtr funchook);

    [DllImport("funchook", CallingConvention = CallingConvention.Cdecl)]
    private static extern FunchookResult funchook_prepare(IntPtr funchook, IntPtr* target_func, IntPtr hook_func);
    [DllImport("funchook", CallingConvention = CallingConvention.Cdecl)]
    private static extern FunchookResult funchook_install(IntPtr funchook, int flags);
    [DllImport("funchook", CallingConvention = CallingConvention.Cdecl)]
    private static extern FunchookResult funchook_uninstall(IntPtr funchook, int flags);

    [DllImport("funchook", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern string funchook_error_message(IntPtr funchook);
    [DllImport("funchook", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern FunchookResult funchook_set_debug_file(string name);
    #endregion
}
