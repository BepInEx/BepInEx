using System;
using System.IO;
using System.Runtime.InteropServices;
using BepInEx.Core;

namespace BepInEx.Unix;

internal static class UnixStreamHelper
{
    public delegate int dupDelegate(int fd);

    public delegate int fcloseDelegate(IntPtr stream);

    public delegate IntPtr fdopenDelegate(int fd, string mode);

    public delegate int fflushDelegate(IntPtr stream);

    public delegate IntPtr freadDelegate(IntPtr ptr, IntPtr size, IntPtr nmemb, IntPtr stream);

    public delegate int fwriteDelegate(IntPtr ptr, IntPtr size, IntPtr nmemb, IntPtr stream);

    public delegate int isattyDelegate(int fd);

    public static dupDelegate dup;

    public static fdopenDelegate fdopen;

    public static freadDelegate fread;

    public static fwriteDelegate fwrite;

    public static fcloseDelegate fclose;

    public static fflushDelegate fflush;

    public static isattyDelegate isatty;

    static UnixStreamHelper()
    {
        string[] libcCandidates = [
                "libc.so.6",               // Ubuntu glibc
                "libc",                    // Linux glibc
                "/usr/lib/libSystem.dylib" // OSX POSIX
        ];

        IntPtr libcHandle = IntPtr.Zero;
        foreach (var libcCandidate in libcCandidates)
        {
            try
            {
                libcHandle = NativeLibrary.Load(libcCandidate);
                if (libcHandle != IntPtr.Zero)
                    break;
            }
            catch { }
        }

        if (libcHandle == IntPtr.Zero)
            throw new DllNotFoundException("Could not load libc.");

        dup = GetDelegate<dupDelegate>(libcHandle, "dup");
        fdopen = GetDelegate<fdopenDelegate>(libcHandle, "fdopen");
        fread = GetDelegate<freadDelegate>(libcHandle, "fread");
        fwrite = GetDelegate<fwriteDelegate>(libcHandle, "fwrite");
        fclose = GetDelegate<fcloseDelegate>(libcHandle, "fclose");
        fflush = GetDelegate<fflushDelegate>(libcHandle, "fflush");
        isatty = GetDelegate<isattyDelegate>(libcHandle, "isatty");
    }
    private static T GetDelegate<T>(IntPtr lib, string name) where T : Delegate
    {
        // TODO: Use dlsym for .net framework compatibility
        // https://github.com/MonoMod/MonoMod.Common/blob/d679ae74d002e513bd88e52091b66283d8537d83/Utils/DynDll.Manual.cs#L257
        return NativeLibrary.GetExport(lib, name).AsDelegate<T>();
    }

    public static Stream CreateDuplicateStream(int fileDescriptor)
    {
        var newFd = dup(fileDescriptor);

        return new UnixStream(newFd, FileAccess.Write);
    }
}
