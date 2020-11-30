using System;
using System.Collections.Generic;
using MonoMod.Utils;

namespace BepInEx.IL2CPP.Allocator
{
	internal class LinuxPageAllocator : UnixPageAllocator
	{
		protected override IMemoryMapper OpenMemoryMap()
		{
			throw new NotImplementedException();
		}

		protected class LinuxMemoryMapper : IMemoryMapper
		{
			public void Dispose()
			{
				throw new NotImplementedException();
			}

			public bool FindNextFree(out IntPtr start, out IntPtr end)
			{
				throw new NotImplementedException();
			}
		}

		static class Unix
		{
			public delegate IntPtr mmapDelegate(IntPtr addr, UIntPtr length, int prot, int flags, int fd, int offset);

			[DynDllImport("mmap")]
			public static mmapDelegate mmap;

			public delegate int munmapDelegate(IntPtr addr, UIntPtr length);

			[DynDllImport("munmap")]
			public static munmapDelegate munmap;

			public delegate IntPtr fdopenDelegate(int fd, string mode);

			[DynDllImport("libc")]
			public static fdopenDelegate fdopen;

			static Unix()
			{
				typeof(Unix).ResolveDynDllImports(new Dictionary<string, List<DynDllMapping>>
				{
					["libc"] = new List<DynDllMapping>
					{
						"libc.so.6", // Ubuntu glibc
						"libc",      // Linux glibc
					}
				});
			}
		}
	}
}
