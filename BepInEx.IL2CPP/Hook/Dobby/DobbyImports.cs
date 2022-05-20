using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BepInEx.IL2CPP.Hook.Dobby;

internal static unsafe class DobbyImports
{
    #region Delegates
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int d_DobbyHook(IntPtr address, IntPtr replace_call, IntPtr* origin_call);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int d_DobbyPrepare(IntPtr address, IntPtr replace_call, IntPtr* origin_call);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int d_DobbyCommit(IntPtr address);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int d_DobbyDestroy(IntPtr address);
    #endregion

    [DynDllImport("dobby")]
    public static d_DobbyHook DobbyHook;

    [DynDllImport("dobby")]
    public static d_DobbyPrepare DobbyPrepare;

    [DynDllImport("dobby")]
    public static d_DobbyCommit DobbyCommit;

    [DynDllImport("dobby")]
    public static d_DobbyDestroy DobbyDestroy;

    static DobbyImports()
    {
        var dobbyImports = new Dictionary<string, List<DynDllMapping>>()
        {
            ["dobby"] = new()
            {
                $"{Paths.BepInExAssemblyDirectory}/native/dobby.dll" // Windows
            }
        };

        typeof(DobbyImports).ResolveDynDllImports(dobbyImports);
    }
}
