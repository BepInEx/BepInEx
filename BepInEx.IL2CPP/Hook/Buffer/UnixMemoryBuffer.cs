using System;

namespace BepInEx.IL2CPP
{
	/// <summary>
	///     Based on https://github.com/kubo/funchook
	/// </summary>
	internal class UnixMemoryBuffer : MemoryBuffer
	{
		public override IntPtr Allocate(IntPtr func)
		{
			throw new NotImplementedException();
		}

		public override void Free(IntPtr buffer)
		{
			throw new NotImplementedException();
		}
	}
}