using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using MonoMod.Utils;

namespace BepInEx.Unix;

internal static class UnixStreamHelper
{
    private static IntPtr libcHandle;
    
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
        libcHandle = DynDll.OpenLibrary(PlatformDetection.OS.Is(OSKind.OSX) ? "/usr/lib/libSystem.dylib" : "libc");
        dup = AsDelegate<dupDelegate>(libcHandle.GetExport("dup"));
        fdopen = AsDelegate<fdopenDelegate>(libcHandle.GetExport("fdopen"));
        fread = AsDelegate<freadDelegate>(libcHandle.GetExport("fread"));
        fwrite = AsDelegate<fwriteDelegate>(libcHandle.GetExport("fwrite"));
        fclose = AsDelegate<fcloseDelegate>(libcHandle.GetExport("fclose"));
        fflush = AsDelegate<fflushDelegate>(libcHandle.GetExport("fflush"));
        isatty = AsDelegate<isattyDelegate>(libcHandle.GetExport("isatty"));    
    }

    private static T AsDelegate<T>(IntPtr s) where T : class => Marshal.GetDelegateForFunctionPointer(s, typeof(T)) as T;
    
    public static Stream CreateDuplicateStream(int fileDescriptor)
    {
        var newFd = dup(fileDescriptor);

        return new UnixStream(newFd, FileAccess.Write);
    }
}
