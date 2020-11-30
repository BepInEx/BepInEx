using System;
using System.Collections.Generic;
using MonoMod.Utils;

namespace BepInEx.IL2CPP.Allocator
{
	/// <summary>
	///     Based on https://github.com/kubo/funchook
	/// </summary>
	internal abstract class UnixPageAllocator : PageAllocator
	{
		protected abstract IMemoryMapper OpenMemoryMap();
		
		public override IntPtr Allocate(IntPtr hint)
		{
			throw new NotImplementedException();
		}

		public override void Free(IntPtr page)
		{
			throw new NotImplementedException();
		}

		protected interface IMemoryMapper: IDisposable
		{
			bool FindNextFree(ref IntPtr start, ref ulong size);
		}
	}
}