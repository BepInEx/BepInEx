using System;
using MonoMod.Utils;

namespace BepInEx.IL2CPP
{
	public abstract class MemoryBuffer
	{
		public abstract IntPtr Allocate(IntPtr func);
		public abstract void Free(IntPtr buffer);

		private static MemoryBuffer instance;
		public static MemoryBuffer Instance => instance ??= Init();

		private static MemoryBuffer Init()
		{
			if (PlatformHelper.Is(Platform.Windows))
				return new WindowsMemoryBuffer();
			if (PlatformHelper.Is(Platform.Unix))
				return new UnixMemoryBuffer();
			throw new NotImplementedException();
		} 
	}
}