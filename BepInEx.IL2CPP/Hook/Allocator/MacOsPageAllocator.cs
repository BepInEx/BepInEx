using System;

namespace BepInEx.IL2CPP.Allocator
{
	internal class MacOsPageAllocator : UnixPageAllocator
	{
		protected override IMemoryMapper OpenMemoryMap()
		{
			throw new NotImplementedException();
		}

		protected class MacOsMemoryMapper : IMemoryMapper
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
	}
}
