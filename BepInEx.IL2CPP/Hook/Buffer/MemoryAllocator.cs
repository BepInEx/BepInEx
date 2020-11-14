using System;
using System.Runtime.CompilerServices;
using MonoMod.Utils;

namespace BepInEx.IL2CPP
{
	/// <summary>
	/// 
	///     Based on https://github.com/kubo/funchook
	/// </summary>
	internal abstract class MemoryAllocator
	{
		/// <summary>
		///     Common page size on Unix and Windows (4k).
		/// </summary>
		protected const int PAGE_SIZE = 0x1000;

		/// <summary>
		///     Allocation granularity on Windows (but can be reused in other implementations).
		/// </summary>
		protected const int ALLOCATION_UNIT = 0x100000;

		private static MemoryAllocator instance;
		public static MemoryAllocator Instance => instance ??= Init();

		public abstract IntPtr Allocate(IntPtr func);
		public abstract void Free(IntPtr buffer);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected static long RoundDown(long num, long unit)
		{
			return num & ~(unit - 1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected static long RoundUp(long num, long unit)
		{
			return (num + unit - 1) & ~ (unit - 1);
		}

		private static MemoryAllocator Init()
		{
			if (PlatformHelper.Is(Platform.Windows))
				return new WindowsMemoryAllocator();
			if (PlatformHelper.Is(Platform.Unix))
				return new UnixMemoryAllocator();
			throw new NotImplementedException();
		}
	}
}