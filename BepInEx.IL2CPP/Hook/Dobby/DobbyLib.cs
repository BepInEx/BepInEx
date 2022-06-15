using System.Runtime.InteropServices;

namespace BepInEx.IL2CPP.Hook.Dobby;

internal static unsafe class DobbyLib
{
    [DllImport("dobby", EntryPoint = "DobbyHook", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Hook(nint target, nint replacement, nint* originalCall);

    [DllImport("dobby", EntryPoint = "DobbyPrepare", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prepare(nint target, nint replacement, nint* originalCall);

    [DllImport("dobby", EntryPoint = "DobbyCommit", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Commit(nint target);

    [DllImport("dobby", EntryPoint = "DobbyDestroy", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Destroy(nint target);
}
