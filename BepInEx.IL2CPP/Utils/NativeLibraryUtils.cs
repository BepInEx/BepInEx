using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BepInEx.IL2CPP.Utils;

internal static class NativeLibraryUtils
{
    static NativeLibraryUtils()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeLibraryUtils).Assembly, Resolver);
    }

    public static event EventHandler<ResolveEvent> OnResolve;

    private static IntPtr Resolver(string libraryname, Assembly assembly, DllImportSearchPath? searchpath)
    {
        var evt = new ResolveEvent { LibraryName = libraryname };
        OnResolve?.Invoke(null, evt);
        return evt.Library;
    }

    public static (string Arch, string LibExt) GetDllIdentifier()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            _                => throw new NotSupportedException()
        };

        string ext;
        if (OperatingSystem.IsWindows())
            ext = "dll";
        else if (OperatingSystem.IsLinux())
            ext = "so";
        else if (OperatingSystem.IsMacOS())
            ext = "dylib";
        else
            throw new NotSupportedException();

        return (arch, ext);
    }

    public class ResolveEvent
    {
        public string LibraryName { get; init; }
        public nint Library { get; set; }
    }
}
