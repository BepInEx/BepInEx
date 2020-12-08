using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MonoMod.Utils;

namespace BepInEx.IL2CPP.Allocator
{
	internal class MacOsPageAllocator : UnixPageAllocator
	{
		protected override IEnumerable<(IntPtr, IntPtr)> MapMemoryAreas()
		{
			var size = IntPtr.Zero;
			var info = new LibSystem.vm_region_basic_info_64();
			var infoCount = (uint)(Marshal.SizeOf<LibSystem.vm_region_basic_info_64>() / sizeof(int));
			var objectName = 0u;
			var address = IntPtr.Zero;

			while (LibSystem.vm_region_64(LibSystem.TaskSelf, ref address, ref size, LibSystem.VM_REGION_BASIC_INFO_64, ref info, ref infoCount, ref objectName) == LibSystem.KERN_SUCCESS)
			{
				var start = new IntPtr(address.ToInt64());
				var end = new IntPtr(address.ToInt64() + size.ToInt64());
				address = end;
				yield return (start, end);
			}
		}
		
		private static class LibSystem
		{
			public static readonly IntPtr TaskSelf;
			public const int VM_REGION_BASIC_INFO_64 = 9;
			public const int KERN_SUCCESS = 0;

			[StructLayout(LayoutKind.Sequential)]
			public struct vm_region_basic_info_64
			{
				public int protection;
				public int max_protection;
				public uint inheritance;
				[MarshalAs(UnmanagedType.I4)]
				public bool shared;
				[MarshalAs(UnmanagedType.I4)]
				public bool reserved;
				public ulong offset;
				public int behavior;
				public ushort user_wired_count;
			}

			public delegate int vm_region_64Delegate(IntPtr target_task, ref IntPtr address, ref IntPtr size, int flavor, ref vm_region_basic_info_64 info, ref uint infoCnt, ref uint object_name);

			[DynDllImport("libSystem")]
			public static vm_region_64Delegate vm_region_64;
			
			static LibSystem()
			{
				typeof(LibSystem).ResolveDynDllImports(new Dictionary<string, List<DynDllMapping>>
				{
					["libSystem"] = new List<DynDllMapping>
					{
						"/usr/lib/libSystem.dylib" // OSX POSIX
					}
				});

				var libsystem = DynDll.OpenLibrary("/usr/lib/libSystem.dylib");
				TaskSelf = libsystem.GetFunction("mach_task_self_"); // This isn't a function but rather an exported symbol
			}
		}
	}
}
