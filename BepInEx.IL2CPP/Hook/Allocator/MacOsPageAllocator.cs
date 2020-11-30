using System;

namespace BepInEx.IL2CPP.Allocator
{
	internal class MacOsPageAllocator : UnixPageAllocator
	{
		protected override IMemoryMapper OpenMemoryMap()
		{
			throw new System.NotImplementedException();
		}
		
		protected class MacOsMemoryMapper : IMemoryMapper
		{
			public void Dispose()
			{
				throw new NotImplementedException();
			}

			public bool FindNextFree(ref IntPtr start, ref ulong size)
			{
				throw new NotImplementedException();
			}
		}
	}
}
