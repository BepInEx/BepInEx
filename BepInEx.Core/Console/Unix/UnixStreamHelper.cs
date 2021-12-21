using System;
using System.Collections.Generic;
using System.IO;
using MonoMod.Utils;

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

    [DynDllImport("libc")]
    public static dupDelegate dup;

    [DynDllImport("libc")]
    public static fdopenDelegate fdopen;

    [DynDllImport("libc")]
    public static freadDelegate fread;

    [DynDllImport("libc")]
    public static fwriteDelegate fwrite;

    [DynDllImport("libc")]
    public static fcloseDelegate fclose;

    [DynDllImport("libc")]
    public static fflushDelegate fflush;

    [DynDllImport("libc")]
    public static isattyDelegate isatty;

    static UnixStreamHelper()
    {
        var libcMapping = new Dictionary<string, List<DynDllMapping>>
        {
            ["libc"] = new()
            {
                "libc.so.6",               // Ubuntu glibc
                "libc",                    // Linux glibc
                "/usr/lib/libSystem.dylib" // OSX POSIX
            }
        };

        typeof(UnixStreamHelper).ResolveDynDllImports(libcMapping);
    }

    public static Stream CreateDuplicateStream(int fileDescriptor)
    {
        var newFd = dup(fileDescriptor);

        return new UnixStream(newFd, FileAccess.Write);
    }
}
