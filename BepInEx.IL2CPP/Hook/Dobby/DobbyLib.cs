using System.IO;
using System.Runtime.InteropServices;
using BepInEx.IL2CPP.Utils;

namespace BepInEx.IL2CPP.Hook.Dobby;

internal static unsafe class DobbyLib
{
    static DobbyLib()
    {
        NativeLibraryUtils.OnResolve += (_, args) =>
        {
            if (args.LibraryName != "dobby")
                return;
            var pathBase = Path.Combine(Paths.BepInExAssemblyDirectory, "native", "dobby");
            var (arch, ext) = NativeLibraryUtils.GetDllIdentifier();
            var libPath = Path.Combine(pathBase, $"dobby_{arch}.{ext}");
            if (NativeLibrary.TryLoad(libPath, out var lib))
                args.Library = lib;
        };
    }

    [DllImport("dobby", EntryPoint = "DobbyHook", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Hook(nint target, nint replacement, nint* originalCall);

    [DllImport("dobby", EntryPoint = "DobbyPrepare", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prepare(nint target, nint replacement, nint* originalCall);

    [DllImport("dobby", EntryPoint = "DobbyCommit", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Commit(nint target);

    [DllImport("dobby", EntryPoint = "DobbyDestroy", CallingConvention = CallingConvention.Cdecl)]
    public static extern int Destroy(nint target);
}
