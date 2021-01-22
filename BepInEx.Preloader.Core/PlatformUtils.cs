using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using MonoMod.Utils;

namespace BepInEx.Preloader.Core
{
    internal static class PlatformUtils
    {
        [DllImport("libc.so.6", EntryPoint = "uname", CallingConvention = CallingConvention.Cdecl,
                   CharSet = CharSet.Ansi)]
        private static extern IntPtr uname_linux(ref utsname_linux utsname);

        [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "uname", CallingConvention = CallingConvention.Cdecl,
                   CharSet = CharSet.Ansi)]
        private static extern IntPtr uname_osx(ref utsname_osx utsname);

        /// <summary>
        ///     Recreation of MonoMod's PlatformHelper.DeterminePlatform method, but with libc calls instead of creating processes.
        /// </summary>
        public static void SetPlatform()
        {
            var current = Platform.Unknown;

            // For old Mono, get from a private property to accurately get the platform.
            // static extern PlatformID Platform
            var p_Platform = typeof(Environment).GetProperty("Platform", BindingFlags.NonPublic | BindingFlags.Static);
            string platID;
            if (p_Platform != null)
                platID = p_Platform.GetValue(null, new object[0]).ToString();
            else
                // For .NET and newer Mono, use the usual value.
                platID = Environment.OSVersion.Platform.ToString();
            platID = platID.ToLowerInvariant();

            if (platID.Contains("win"))
                current = Platform.Windows;
            else if (platID.Contains("mac") || platID.Contains("osx"))
                current = Platform.MacOS;
            else if (platID.Contains("lin") || platID.Contains("unix")) current = Platform.Linux;

            if (Is(current, Platform.Linux) && Directory.Exists("/data") && File.Exists("/system/build.prop"))
                current = Platform.Android;
            else if (Is(current, Platform.Unix) && Directory.Exists("/System/Library/AccessibilityBundles"))
                current = Platform.iOS;

            // Is64BitOperatingSystem has been added in .NET Framework 4.0
            var m_get_Is64BitOperatingSystem =
                typeof(Environment).GetProperty("Is64BitOperatingSystem")?.GetGetMethod();
            if (m_get_Is64BitOperatingSystem != null)
                current |= (bool) m_get_Is64BitOperatingSystem.Invoke(null, new object[0]) ? Platform.Bits64 : 0;
            else
                current |= IntPtr.Size >= 8 ? Platform.Bits64 : 0;

            if ((Is(current, Platform.MacOS) || Is(current, Platform.Linux)) && Type.GetType("Mono.Runtime") != null)
            {
                string arch;
                IntPtr result;

                if (Is(current, Platform.MacOS))
                {
                    var utsname_osx = new utsname_osx();
                    result = uname_osx(ref utsname_osx);
                    arch = utsname_osx.machine;
                }
                else
                {
                    // Linux
                    var utsname_linux = new utsname_linux();
                    result = uname_linux(ref utsname_linux);
                    arch = utsname_linux.machine;
                }

                if (result == IntPtr.Zero && (arch.StartsWith("aarch") || arch.StartsWith("arm")))
                    current |= Platform.ARM;
            }
            else
            {
                // Detect ARM based on PE info or uname.
                typeof(object).Module.GetPEKind(out var peKind, out var machine);
                if (machine == (ImageFileMachine) 0x01C4 /* ARM, .NET Framework 4.5 */)
                    current |= Platform.ARM;
            }

            PlatformHelper.Current = current;
        }

        private static bool Is(Platform current, Platform expected) => (current & expected) == expected;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct utsname_osx
        {
            private const int osx_utslen = 256;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = osx_utslen)]
            public string sysname;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = osx_utslen)]
            public string nodename;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = osx_utslen)]
            public string release;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = osx_utslen)]
            public string version;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = osx_utslen)]
            public string machine;
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
}
