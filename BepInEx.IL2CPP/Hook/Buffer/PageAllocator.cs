using System;
using System.Runtime.CompilerServices;
using MonoMod.Utils;

namespace BepInEx.IL2CPP
{
	/// <summary>
	///     A general purpose page allocator for patching purposes.
	///     Allows to allocate pages (4k memory chunks) within the 1GB radius of a given address.
	/// </summary>
	/// <remarks>Based on https://github.com/kubo/funchook</remarks>
	internal abstract class PageAllocator
	{
		/// <summary>
		///     Common page size on Unix and Windows (4k).
		/// </summary>
		public const int PAGE_SIZE = 0x1000;

		/// <summary>
		///     Allocation granularity on Windows (but can be reused in other implementations).
		/// </summary>
		protected const int ALLOCATION_UNIT = 0x100000;

		protected const int PAGES_PER_UNIT = ALLOCATION_UNIT / PAGE_SIZE;

		private static PageAllocator instance;
		public static PageAllocator Instance => instance ??= Init();

		public abstract IntPtr Allocate(IntPtr hint);

		public abstract void Free(IntPtr page);

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected static bool IsInRelJmpRange(IntPtr src, IntPtr dst)
		{
			long diff = dst.ToInt64() - src.ToInt64();
			return int.MinValue <= diff && diff <= int.MaxValue;
		}

		private static PageAllocator Init()
		{
			if (PlatformHelper.Is(Platform.Windows))
				return new WindowsPageAllocator();
			if (PlatformHelper.Is(Platform.Unix))
				return new UnixPageAllocator();
			throw new NotImplementedException();
		}
	}
}