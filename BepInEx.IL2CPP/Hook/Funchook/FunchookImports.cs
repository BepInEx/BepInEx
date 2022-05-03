using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BepInEx.IL2CPP.Hook.Funchook;

internal static unsafe class FunchookImports
{
    #region Delegates
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] 
    public delegate IntPtr d_funchook_create();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] 
    public delegate FunchookResult d_funchook_destroy(IntPtr funchook);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] 
    public delegate FunchookResult d_funchook_prepare(IntPtr funchook, IntPtr* target_func, IntPtr hook_func);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] 
    public delegate FunchookResult d_funchook_install(IntPtr funchook, int flags);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] 
    public delegate FunchookResult d_funchook_uninstall(IntPtr funchook, int flags);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)] 
    public delegate string d_funchook_error_message(IntPtr funchook);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)] 
    public delegate FunchookResult d_funchook_set_debug_file([MarshalAs(UnmanagedType.LPStr)] string name);
    #endregion

    [DynDllImport("funchook")]
    public static d_funchook_create funchook_create;

    [DynDllImport("funchook")]
    public static d_funchook_destroy funchook_destroy;

    [DynDllImport("funchook")]
    public static d_funchook_prepare funchook_prepare;

    [DynDllImport("funchook")]
    public static d_funchook_install funchook_install;

    [DynDllImport("funchook")]
    public static d_funchook_uninstall funchook_uninstall;

    [DynDllImport("funchook")]
    public static d_funchook_error_message funchook_error_message;

    [DynDllImport("funchook")]
    public static d_funchook_set_debug_file funchook_set_debug_file;

    static FunchookImports()
    {
        var funchookImports = new Dictionary<string, List<DynDllMapping>>()
        {
            ["funchook"] = new()
            {
                $"{Paths.BepInExAssemblyDirectory}/native/funchook.dll" // Windows
            }
        };

        typeof(FunchookImports).ResolveDynDllImports(funchookImports);
    }
}
