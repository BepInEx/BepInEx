using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BepInEx.Logging;

namespace BepInEx.IL2CPP.Hook.Allocator;

/// <summary>
///     Based on https://github.com/kubo/funchook
/// </summary>
internal class WindowsPageAllocator : PageAllocator
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static nint RoundUp(nint num, nint unit) => (num + unit - 1) & ~(unit - 1);

    private static Win32Exception Win32Error(string message = null, int code = -1)
    {
        var ex = code >= 0 ? new Win32Exception(code) : new Win32Exception(); // Generate error message
        return new Win32Exception(ex.NativeErrorCode,
                                  !string.IsNullOrWhiteSpace(message) ? $"{message}: {ex.Message}" : ex.Message);
    }

    protected override nint AllocateChunk(nint hint)
    {
        nint chunk;
        while (true)
        {
            if (WinApi.VirtualQuery(hint, out var mbi) == 0)
            {
                Logger.Log(LogLevel.Debug,
                           $"Skipping analysing 0x{(long) hint:X8} because VirtualQuery failed: {Win32Error()}");
                goto next;
            }

            if (mbi.State == WinApi.PageState.MEM_FREE)
            {
                var nextAddress = RoundUp(mbi.BaseAddress, ALLOCATION_UNIT);
                var d = nextAddress - mbi.BaseAddress;
                if (d >= 0 && mbi.RegionSize - d >= ALLOCATION_UNIT)
                {
                    Logger.Log(LogLevel.Debug, $"Found free chunk at 0x{(long) nextAddress:X8}");
                    hint = nextAddress;

                    chunk = WinApi.VirtualAlloc(hint, ALLOCATION_UNIT, WinApi.AllocationType.MEM_RESERVE,
                                                WinApi.ProtectConstant.PAGE_NOACCESS);
                    if (chunk == 0)
                    {
                        Logger.Log(LogLevel.Debug, $"Failed to reserve address: 0x{(long) hint:X8}: {Win32Error()}");
                        goto next;
                    }

                    break;
                }
            }

            next:
            hint = mbi.BaseAddress + mbi.RegionSize;
        }

        var addr = WinApi.VirtualAlloc(chunk, PAGE_SIZE, WinApi.AllocationType.MEM_COMMIT,
                                       WinApi.ProtectConstant.PAGE_READWRITE);
        if (addr == 0)
        {
            var error = Marshal.GetLastWin32Error();
            WinApi.VirtualFree(chunk, 0, WinApi.FreeType.MEM_RELEASE);
            throw
                Win32Error($"Failed to commit memory 0x{(long) addr:X8} for read-write access for 0x{(long) chunk:X8}",
                           error);
        }

        return chunk;
    }

    public override nint Allocate(nint hint)
    {
        var pageAddress = base.Allocate(hint);
        if (WinApi.VirtualAlloc(pageAddress, PAGE_SIZE, WinApi.AllocationType.MEM_COMMIT,
                                WinApi.ProtectConstant.PAGE_READWRITE) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return pageAddress;
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

        [DllImport("kernel32", EntryPoint = "VirtualQuery", SetLastError = true)]
        static extern int VirtualQuery32(nint lpAddress, ref MEMORY_BASIC_INFORMATION lpBuffer, int dwLength);

        [DllImport("kernel32", EntryPoint = "VirtualQuery", SetLastError = true)]
        static extern int VirtualQuery64(nint lpAddress, ref MEMORY_BASIC_INFORMATION64 lpBuffer, int dwLength);

        public static int VirtualQuery(nint lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer)
        {
            lpBuffer = new MEMORY_BASIC_INFORMATION();
            if (Environment.Is64BitProcess)
            {
                var mbi = new MEMORY_BASIC_INFORMATION64();
                var res = VirtualQuery64(lpAddress, ref mbi, Marshal.SizeOf<MEMORY_BASIC_INFORMATION64>());
                lpBuffer.BaseAddress = mbi.BaseAddress;
                lpBuffer.AllocationBase = mbi.AllocationBase;
                lpBuffer.AllocationProtect = mbi.AllocationProtect;
                lpBuffer.RegionSize = mbi.RegionSize;
                lpBuffer.State = mbi.State;
                lpBuffer.Protect = mbi.Protect;
                lpBuffer.Type = mbi.Type;
                return res;
            }

            return VirtualQuery32(lpAddress, ref lpBuffer, Marshal.SizeOf<MEMORY_BASIC_INFORMATION>());
        }

        [DllImport("kernel32", SetLastError = true)]
        public static extern nint VirtualAlloc(nint lpAddress,
                                               nuint dwSize,
                                               AllocationType flAllocationType,
                                               ProtectConstant flProtect);

        [DllImport("kernel32", SetLastError = true)]
        public static extern bool VirtualFree(nint lpAddress, nuint dwSize, FreeType dwFreeType);

        [StructLayout(LayoutKind.Sequential)]
        // ReSharper disable once InconsistentNaming
        public struct MEMORY_BASIC_INFORMATION
        {
            public nint BaseAddress;
            public nint AllocationBase;
            public uint AllocationProtect;
            public nint RegionSize;
            public PageState State;
            public uint Protect;
            public uint Type;
        }

        [StructLayout(LayoutKind.Sequential)]
        // ReSharper disable once InconsistentNaming
        struct MEMORY_BASIC_INFORMATION64
        {
            public nint BaseAddress;
            public nint AllocationBase;
            public uint AllocationProtect;
            public uint __alignment1;
            public nint RegionSize;
            public PageState State;
            public uint Protect;
            public uint Type;
            public int __alignment2;
        }
    }
}
