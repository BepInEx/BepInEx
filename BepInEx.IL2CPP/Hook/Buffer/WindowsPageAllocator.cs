using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BepInEx.IL2CPP
{
	/// <summary>
	///     Based on https://github.com/kubo/funchook
	/// </summary>
	internal class WindowsPageAllocator : PageAllocator
	{
		private readonly List<PageChunk> allocatedChunks = new List<PageChunk>();

		/// <summary>
		///     Allocates a single 64k chunk of memory near the given address
		/// </summary>
		/// <param name="hint">Address near which to attempt allocate the chunk</param>
		/// <returns>Allocated chunk</returns>
		/// <exception cref="Win32Exception">Allocation failed</exception>
		private PageChunk AllocateChunk(IntPtr hint)
		{
			while (true)
			{
				var mbi = new WinApi.MEMORY_BASIC_INFORMATION();
				if (WinApi.VirtualQuery(hint, ref mbi, Marshal.SizeOf<WinApi.MEMORY_BASIC_INFORMATION>()) == 0)
					throw new Win32Exception(Marshal.GetLastWin32Error());

				if (mbi.State == WinApi.PageState.MEM_FREE)
				{
					long nextAddress = RoundUp(mbi.BaseAddress.ToInt64(), ALLOCATION_UNIT);
					long d = nextAddress - mbi.BaseAddress.ToInt64();
					if (d >= 0 && mbi.RegionSize.ToInt64() - d >= ALLOCATION_UNIT)
					{
						hint = (IntPtr)nextAddress;
						break;
					}
				}

				hint = (IntPtr)(mbi.BaseAddress.ToInt64() + mbi.RegionSize.ToInt64());
			}

			var chunk = WinApi.VirtualAlloc(hint, (UIntPtr)ALLOCATION_UNIT, WinApi.AllocationType.MEM_RESERVE, WinApi.ProtectConstant.PAGE_NOACCESS);
			if (chunk == null)
				throw new Win32Exception(Marshal.GetLastWin32Error());
			var addr = WinApi.VirtualAlloc(chunk, (UIntPtr)PAGE_SIZE, WinApi.AllocationType.MEM_COMMIT, WinApi.ProtectConstant.PAGE_READWRITE);
			if (addr == IntPtr.Zero)
			{
				int error = Marshal.GetLastWin32Error();
				WinApi.VirtualFree(chunk, UIntPtr.Zero, WinApi.FreeType.MEM_RELEASE);
				throw new Win32Exception(error);
			}

			var result = new PageChunk
			{
				BaseAddress = chunk
			};
			allocatedChunks.Add(result);
			return result;
		}

		private IntPtr AllocPage(IntPtr hint)
		{
			foreach (var allocatedChunk in allocatedChunks)
			{
				// Small shortcut to speed up page lookup
				if (allocatedChunk.UsedPages == PAGES_PER_UNIT)
					continue;
				for (var i = 0; i < allocatedChunk.Pages.Length; i++)
				{
					if (allocatedChunk.Pages[i])
						continue;
					var pageAddr = allocatedChunk.GetPage(i);
					if (!IsInRelJmpRange(hint, pageAddr))
						continue;
					allocatedChunk.Pages[i] = true;
					allocatedChunk.UsedPages++;
					return pageAddr;
				}
			}

			var chunk = AllocateChunk(hint);
			chunk.Pages[0] = true;
			chunk.UsedPages++;
			return chunk.BaseAddress;
		}

		public override IntPtr Allocate(IntPtr hint)
		{
			var pageAddress = AllocPage(hint);
			if (WinApi.VirtualAlloc(pageAddress, (UIntPtr)PAGE_SIZE, WinApi.AllocationType.MEM_COMMIT, WinApi.ProtectConstant.PAGE_READWRITE) == IntPtr.Zero)
				throw new Win32Exception(Marshal.GetLastWin32Error());
			return pageAddress;
		}

		public override void Free(IntPtr page)
		{
			foreach (var allocatedChunk in allocatedChunks)
			{
				long index = (page.ToInt64() - allocatedChunk.BaseAddress.ToInt64()) / PAGE_SIZE;
				if (index < 0 || index > PAGES_PER_UNIT)
					continue;
				allocatedChunk.Pages[index] = false;
				return;
			}
		}

		private class PageChunk
		{
			public IntPtr BaseAddress;
			public readonly bool[] Pages = new bool[PAGES_PER_UNIT];
			public int UsedPages;

			public IntPtr GetPage(int index)
			{
				return BaseAddress + index * PAGE_SIZE;
			}
		}

		private static class WinApi
		{
			[Flags]
			public enum AllocationType : uint
			{
				// ReSharper disable InconsistentNaming
				MEM_COMMIT = 0x00001000,

				MEM_RESERVE = 0x00002000
				// ReSharper restore InconsistentNaming
			}


			[Flags]
			public enum FreeType : uint
			{
				// ReSharper disable InconsistentNaming
				MEM_RELEASE = 0x00008000
				// ReSharper restore InconsistentNaming
			}

			public enum PageState : uint
			{
				// ReSharper disable InconsistentNaming
				MEM_FREE = 0x10000
				// ReSharper restore InconsistentNaming
			}

			[Flags]
			public enum ProtectConstant : uint
			{
				// ReSharper disable InconsistentNaming
				PAGE_NOACCESS = 0x01,

				PAGE_READWRITE = 0x04
				// ReSharper restore InconsistentNaming
			}

			[DllImport("kernel32", SetLastError = true)]
			public static extern int VirtualQuery(IntPtr lpAddress, ref MEMORY_BASIC_INFORMATION lpBuffer, int dwLength);

			[DllImport("kernel32", SetLastError = true)]
			public static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize, AllocationType flAllocationType, ProtectConstant flProtect);

			[DllImport("kernel32", SetLastError = true)]
			public static extern bool VirtualFree(IntPtr lpAddress, UIntPtr dwSize, FreeType dwFreeType);

			[StructLayout(LayoutKind.Sequential)]
			// ReSharper disable once InconsistentNaming
			public struct MEMORY_BASIC_INFORMATION
			{
				public readonly IntPtr BaseAddress;
				public readonly IntPtr AllocationBase;
				public readonly uint AllocationProtect;
				public readonly IntPtr RegionSize;
				public readonly PageState State;
				public readonly uint Protect;
				public readonly uint Type;
			}
		}
	}
}