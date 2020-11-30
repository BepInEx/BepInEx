using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MonoMod.Utils;

namespace BepInEx.IL2CPP.Allocator
{
	/// <summary>
	///     Based on https://github.com/kubo/funchook
	/// </summary>
	internal abstract class UnixPageAllocator : PageAllocator
	{
		protected abstract IMemoryMapper OpenMemoryMap();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool CheckFreeRegionBefore(IntPtr start, IntPtr hint, IntPtr[] result)
		{
			if (start.ToInt64() < hint.ToInt64())
			{
				var addr = start - PAGE_SIZE;
				if (hint.ToInt64() - addr.ToInt64() < int.MaxValue)
					result[0] = addr;
			}

			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool CheckFreeRegionAfter(IntPtr end, IntPtr hint, IntPtr[] result)
		{
			if (hint.ToInt64() < end.ToInt64())
			{
				if (end.ToInt64() - hint.ToInt64() < int.MaxValue)
					result[1] = end;
				return true;
			}

			return false;
		}

		private IntPtr[] GetFreeAddresses(IntPtr hint)
		{
			var result = new IntPtr[2];
			var prevEnd = IntPtr.Zero;
			using var mapper = OpenMemoryMap();

			while (mapper.FindNextFree(out var start, out var end))
			{
				if ((prevEnd + PAGE_SIZE).ToInt64() <= start.ToInt64())
					if (CheckFreeRegionBefore(start, hint, result) || CheckFreeRegionAfter(prevEnd, hint, result))
						return result;
				prevEnd = end;
			}

			if (CheckFreeRegionAfter(prevEnd, hint, result))
				return result;
			throw new PageAllocatorException($"Could not find free region near {hint.ToInt64():X8}");
		}

		protected override IntPtr AllocateChunk(IntPtr hint)
		{
			throw new NotImplementedException();
		}

		protected interface IMemoryMapper : IDisposable
		{
			bool FindNextFree(out IntPtr start, out IntPtr end);
		}
	}
}
