using System.Runtime.InteropServices;
using BepInEx.IL2CPP.Utils;
using Il2CppSystem.IO;

namespace BepInEx.IL2CPP.Hook.Funchook;

internal enum FunchookResult
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
    NoAvailableRegisters = 11
}

internal static unsafe class FunchookLib
{
    [DllImport("funchook", EntryPoint = "funchook_create", CallingConvention = CallingConvention.Cdecl)]
    public static extern nint Create();

    [DllImport("funchook", EntryPoint = "funchook_destroy", CallingConvention = CallingConvention.Cdecl)]
    public static extern FunchookResult Destroy(nint handle);

    [DllImport("funchook", EntryPoint = "funchook_prepare", CallingConvention = CallingConvention.Cdecl)]
    public static extern FunchookResult Prepare(nint handle, nint* target, nint hook);

    [DllImport("funchook", EntryPoint = "funchook_install", CallingConvention = CallingConvention.Cdecl)]
    public static extern FunchookResult Install(nint handle, int flags);

    [DllImport("funchook", EntryPoint = "funchook_uninstall", CallingConvention = CallingConvention.Cdecl)]
    public static extern FunchookResult Uninstall(nint handle, int flags);

    [DllImport("funchook", EntryPoint = "funchook_error_message", CallingConvention = CallingConvention.Cdecl)]
    public static extern string ErrorMessage(nint handle);

    [DllImport("funchook", EntryPoint = "funchook_set_debug_file", CallingConvention = CallingConvention.Cdecl)]
    public static extern FunchookResult SetDebugFile([MarshalAs(UnmanagedType.LPStr)] string name);
}
