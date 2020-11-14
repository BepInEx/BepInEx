using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BepInEx.IL2CPP
{
	/// <summary>
	///     Based on https://github.com/kubo/funchook
	/// </summary>
	internal class WindowsMemoryBuffer : MemoryBuffer
	{
		private readonly LinkedList<IntPtr> allocatedChunks = new LinkedList<IntPtr>();

		/// <summary>
		///     Allocates a single 64k chunk of memory near the given address
		/// </summary>
		/// <param name="hint">Address near which to attempt allocate the chunk</param>
		/// <returns>Allocated chunk</returns>
		/// <exception cref="Win32Exception">Allocation failed</exception>
		private IntPtr AllocateChunk(IntPtr hint)
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

			allocatedChunks.AddFirst(chunk);
			return chunk;
		}

		public override IntPtr Allocate(IntPtr func)
		{
			throw new NotImplementedException();
		}

		public override void Free(IntPtr buffer)
		{
			throw new NotImplementedException();
		}

		private static class WinApi
		{
			[Flags]
			public enum AllocationType : uint
			{
				// ReSharper disable InconsistentNaming
				MEM_COMMIT = 0x00001000,
				MEM_RESERVE = 0x00002000,
				MEM_RESET = 0x00080000,
				MEM_RESET_UNDO = 0x1000000,
				MEM_LARGE_PAGES = 0x20000000,
				MEM_PHYSICAL = 0x00400000,
				MEM_TOP_DOWN = 0x00100000,

				MEM_WRITE_WATCH = 0x00200000
				// ReSharper restore InconsistentNaming
			}


			[Flags]
			public enum FreeType : uint
			{
				// ReSharper disable InconsistentNaming
				MEM_DECOMMIT = 0x00004000,
				MEM_RELEASE = 0x00008000,
				MEM_COALESCE_PLACEHOLDERS = 0x00000001,

				MEM_PRESERVE_PLACEHOLDER = 0x00000002
				// ReSharper restore InconsistentNaming
			}

			public enum PageState : uint
			{
				// ReSharper disable InconsistentNaming
				MEM_COMMIT = 0x1000,
				MEM_FREE = 0x10000,

				MEM_RESERVE = 0x2000
				// ReSharper restore InconsistentNaming
			}

			[Flags]
			public enum ProtectConstant : uint
			{
				// ReSharper disable InconsistentNaming
				PAGE_EXECUTE = 0x10,
				PAGE_EXECUTE_READ = 0x20,
				PAGE_EXECUTE_READWRITE = 0x40,
				PAGE_EXECUTE_WRITECOPY = 0x80,
				PAGE_NOACCESS = 0x01,
				PAGE_READONLY = 0x02,
				PAGE_READWRITE = 0x04,
				PAGE_WRITECOPY = 0x08,
				PAGE_TARGETS_INVALID = 0x40000000,
				PAGE_TARGETS_NO_UPDATE = 0x40000000,
				PAGE_GUARD = 0x100,
				PAGE_NOCACHE = 0x200,

				PAGE_WRITECOMBINE = 0x400
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