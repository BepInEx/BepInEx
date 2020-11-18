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
		///     Call to <see cref="Allocate" /> will allocate a single page of this size.
		/// </summary>
		public const int PAGE_SIZE = 0x1000;

		/// <summary>
		///     Allocation granularity on Windows (but can be reused in other implementations).
		/// </summary>
		protected const int ALLOCATION_UNIT = 0x100000;

		protected const int PAGES_PER_UNIT = ALLOCATION_UNIT / PAGE_SIZE;

		private static PageAllocator instance;

		/// <summary>
		///     Platform-specific instance of page allocator.
		/// </summary>
		public static PageAllocator Instance => instance ??= Init();

		/// <summary>
		///     Allocates a single page of size <see cref="PAGE_SIZE" /> near the provided address.
		///     Attempts to allocate the page within the +-1GB region of the hinted address.
		/// </summary>
		/// <param name="hint">Address near which to attempt to allocate the page.</param>
		/// <returns>Address to the allocated page.</returns>
		public abstract IntPtr Allocate(IntPtr hint);

		/// <summary>
		///     Frees the page allocated with <see cref="Allocate" />
		/// </summary>
		/// <param name="page"></param>
		public abstract void Free(IntPtr page);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected static long RoundUp(long num, long unit)
		{
			return (num + unit - 1) & ~ (unit - 1);
		}

		/// <summary>
		///     Checks if the given address is within the relative jump range.
		/// </summary>
		/// <param name="src">Source address to jump from.</param>
		/// <param name="dst">Destination address to jump to.</param>
		/// <returns>True, if the distance between the addresses is within the relative jump range (usually 1GB), otherwise false.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsInRelJmpRange(IntPtr src, IntPtr dst)
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