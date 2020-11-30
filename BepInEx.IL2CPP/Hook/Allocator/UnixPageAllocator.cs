using System;

namespace BepInEx.IL2CPP
{
	/// <summary>
	///     Based on https://github.com/kubo/funchook
	/// </summary>
	internal class UnixPageAllocator : PageAllocator
	{
		public override IntPtr Allocate(IntPtr hint)
		{
			throw new NotImplementedException();
		}

		public override void Free(IntPtr page)
		{
			throw new NotImplementedException();
		}
	}
}