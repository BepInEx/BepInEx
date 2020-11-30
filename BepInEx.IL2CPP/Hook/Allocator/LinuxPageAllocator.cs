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

		private static class Unix
		{
			[DynDllImport("mmap")]
			public static mmapDelegate mmap;

			[DynDllImport("munmap")]
			public static munmapDelegate munmap;

			[DynDllImport("libc")]
			public static fdopenDelegate fdopen;

			public delegate IntPtr fdopenDelegate(int fd, string mode);
			public delegate IntPtr mmapDelegate(IntPtr addr, UIntPtr length, int prot, int flags, int fd, int offset);
			public delegate int munmapDelegate(IntPtr addr, UIntPtr length);

			static Unix()
			{
				typeof(Unix).ResolveDynDllImports(new Dictionary<string, List<DynDllMapping>>
				{
					["libc"] = new List<DynDllMapping>
					{
						"libc.so.6", // Ubuntu glibc
						"libc"       // Linux glibc
					}
				});
			}
		}
	}
}
