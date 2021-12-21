using System.Collections.Generic;
using System.Runtime.InteropServices;
using MonoMod.Utils;

namespace BepInEx.IL2CPP.Hook.Allocator;

internal class MacOsPageAllocator : UnixPageAllocator
{
    protected override IEnumerable<(nint, nint)> MapMemoryAreas()
    {
        var info = new LibSystem.vm_region_basic_info_64();
        var infoCount = (uint) (Marshal.SizeOf<LibSystem.vm_region_basic_info_64>() / sizeof(int));
        var objectName = 0u;
        nint address = 0;
        nint size = 0;

        while (LibSystem.vm_region_64(LibSystem.TaskSelf, ref address, ref size, LibSystem.VM_REGION_BASIC_INFO_64,
                                      ref info, ref infoCount, ref objectName) == LibSystem.KERN_SUCCESS)
        {
            var start = address;
            var end = address + size;
            address = end;
            yield return (start, end);
        }
    }

    private static class LibSystem
    {
        public const int VM_REGION_BASIC_INFO_64 = 9;
        public const int KERN_SUCCESS = 0;
        public static readonly nint TaskSelf;

        static LibSystem()
        {
            typeof(LibSystem).ResolveDynDllImports(new Dictionary<string, List<DynDllMapping>>
            {
                ["libSystem"] = new()
                {
                    "/usr/lib/libSystem.dylib" // OSX POSIX
                }
            });

            var libsystem = DynDll.OpenLibrary("/usr/lib/libSystem.dylib");
            TaskSelf = libsystem
                .GetFunction("mach_task_self_"); // This isn't a function but rather an exported symbol
        }

        // ReSharper disable InconsistentNaming
        [StructLayout(LayoutKind.Sequential)]
        public readonly struct vm_region_basic_info_64
        {
            public readonly int protection;
            public readonly int max_protection;
            public readonly uint inheritance;

            [MarshalAs(UnmanagedType.I4)]
            public readonly bool shared;

            [MarshalAs(UnmanagedType.I4)]
            public readonly bool reserved;

            public readonly ulong offset;
            public readonly int behavior;
            public readonly ushort user_wired_count;
        }
        // ReSharper restore InconsistentNaming

        // ReSharper disable InconsistentNaming
#pragma warning disable 649 // Set by MonoMod
        [DynDllImport("libSystem")]
        public static vm_region_64Delegate vm_region_64;
#pragma warning restore 649

        public delegate int vm_region_64Delegate(nint target_task,
                                                 ref nint address,
                                                 ref nint size,
                                                 int flavor,
                                                 ref vm_region_basic_info_64 info,
                                                 ref uint infoCnt,
                                                 ref uint object_name);
        // ReSharper restore InconsistentNaming
    }
}
