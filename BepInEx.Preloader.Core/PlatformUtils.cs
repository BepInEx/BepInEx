using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using MonoMod.Utils;

namespace BepInEx.Preloader.Core;

internal static class PlatformUtils
{
    public static Version WindowsVersion { get; set; }
    public static string WineVersion { get; set; }
    public static string LinuxKernelVersion { get; set; }

    [DllImport("libc.so.6", EntryPoint = "uname", CallingConvention = CallingConvention.Cdecl,
               CharSet = CharSet.Ansi)]
    private static extern IntPtr uname_linux(ref utsname_linux utsname);

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern bool RtlGetVersion(ref WindowsOSVersionInfoExW versionInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadLibrary(string libraryName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    
    private static T AsDelegate<T>(IntPtr s) where T : class => Marshal.GetDelegateForFunctionPointer(s, typeof(T)) as T;
    
    public static void SetPlatformVersion()
    {
        if (PlatformDetection.OS.Is(OSKind.Windows))
        {
            var windowsVersionInfo = new WindowsOSVersionInfoExW();
            RtlGetVersion(ref windowsVersionInfo);

            WindowsVersion = new Version((int) windowsVersionInfo.dwMajorVersion,
                                         (int) windowsVersionInfo.dwMinorVersion, 0,
                                         (int) windowsVersionInfo.dwBuildNumber);

            var ntDll = LoadLibrary("ntdll.dll");
            if (ntDll != IntPtr.Zero)
            {
                var wineGetVersion = GetProcAddress(ntDll, "wine_get_version");
                if (wineGetVersion != IntPtr.Zero)
                {
                    var getVersion = AsDelegate<GetWineVersionDelegate>(wineGetVersion);
                    WineVersion = getVersion();
                }
            }
        }

        if (PlatformDetection.OS.Is(OSKind.Linux))
        {
            var utsname_linux = new utsname_linux();
            IntPtr result = uname_linux(ref utsname_linux);
            if (result != IntPtr.Zero)
            {
                LinuxKernelVersion = utsname_linux.version;
            }
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.LPStr)]
    private delegate string GetWineVersionDelegate();

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public struct WindowsOSVersionInfoExW
    {
        public uint dwOSVersionInfoSize;
        public uint dwMajorVersion;
        public uint dwMinorVersion;
        public uint dwBuildNumber;
        public uint dwPlatformId;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szCSDVersion;

        public ushort wServicePackMajor;
        public ushort wServicePackMinor;
        public ushort wSuiteMask;
        public byte wProductType;
        public byte wReserved;

        public WindowsOSVersionInfoExW()
        {
            dwOSVersionInfoSize = (uint) Marshal.SizeOf(typeof(WindowsOSVersionInfoExW));
            dwMajorVersion = 0;
            dwMinorVersion = 0;
            dwBuildNumber = 0;
            dwPlatformId = 0;
            szCSDVersion = null;
            wServicePackMajor = 0;
            wServicePackMinor = 0;
            wSuiteMask = 0;
            wProductType = 0;
            wReserved = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct utsname_linux
    {
        private const int linux_utslen = 65;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = linux_utslen)]
        public string sysname;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = linux_utslen)]
        public string nodename;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = linux_utslen)]
        public string release;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = linux_utslen)]
        public string version;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = linux_utslen)]
        public string machine;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = linux_utslen)]
        public string domainname;
    }
}
