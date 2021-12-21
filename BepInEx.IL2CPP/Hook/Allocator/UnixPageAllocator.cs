using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MonoMod.Utils;

namespace BepInEx.IL2CPP.Hook.Allocator;

/// <summary>
///     Based on https://github.com/kubo/funchook
/// </summary>
internal abstract class UnixPageAllocator : PageAllocator
{
    protected abstract IEnumerable<(nint, nint)> MapMemoryAreas();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CheckFreeRegionBefore(nint start, nint hint, ref (nint Start, nint End) region)
    {
        if (start < hint)
        {
            var addr = start - PAGE_SIZE;
            if (hint - addr < int.MaxValue)
                region.Start = addr;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CheckFreeRegionAfter(nint end, nint hint, ref (nint Start, nint End) region)
    {
        if (hint < end)
        {
            if (end - hint < int.MaxValue)
                region.End = end;
            return true;
        }

        return false;
    }

    private (nint, nint) GetFreeArea(nint hint)
    {
        (nint, nint) result = (0, 0);
        nint prevEnd = 0;

        foreach (var (start, end) in MapMemoryAreas())
        {
            if (prevEnd + PAGE_SIZE <= start)
                if (CheckFreeRegionBefore(start, hint, ref result) ||
                    CheckFreeRegionAfter(prevEnd, hint, ref result))
                    return result;
            prevEnd = end;
        }

        if (CheckFreeRegionAfter(prevEnd, hint, ref result))
            return result;
        throw new PageAllocatorException($"Could not find free region near {(long) hint:X8}");
    }

    protected override nint AllocateChunk(nint hint)
    {
        /* From https://github.com/kubo/funchook/blob/master/src/funchook_unix.c#L251-L254:
         * Loop three times just to avoid rare cases such as
         * unused memory region is used between 'get_free_address()'
         * and 'mmap()'.
        */
        const int retryCount = 3;

        for (var attempt = 0; attempt < retryCount; attempt++)
        {
            var (start, end) = GetFreeArea(hint);
            // Try to allocate to end (after original method) first, then try before
            var addrs = new[] { end, start };
            foreach (var addr in addrs)
            {
                if (addr == 0)
                    continue;
                var result = Unix.mmap(addr, PAGE_SIZE, Unix.Protection.PROT_READ | Unix.Protection.PROT_WRITE,
                                       Unix.MapFlags.MAP_PRIVATE | Unix.MapFlags.MAP_ANONYMOUS, -1, 0);
                if (result == addr)
                    return result;
                if (result == Unix.MAP_FAILED)
                    throw new Win32Exception(Marshal.GetLastWin32Error()); // Yes, this should work on unix too
                Unix.munmap(result, PAGE_SIZE);
            }
        }

        throw new PageAllocatorException("Failed to allocate memory in unused regions");
    }

    private static class Unix
    {
        public delegate nint mmapDelegate(nint addr,
                                          nuint length,
                                          Protection prot,
                                          MapFlags flags,
                                          int fd,
                                          int offset);

        public delegate int munmapDelegate(nint addr, nuint length);

        [Flags]
        public enum MapFlags
        {
            MAP_PRIVATE = 0x02,
            MAP_ANONYMOUS = 0x20
        }

        [Flags]
        public enum Protection
        {
            PROT_READ = 0x1,
            PROT_WRITE = 0x2
        }

        public static readonly nint MAP_FAILED = -1;

        static Unix()
        {
            typeof(Unix).ResolveDynDllImports(new Dictionary<string, List<DynDllMapping>>
            {
                ["libc"] = new()
                {
                    "libc.so.6",               // Ubuntu glibc
                    "libc",                    // Linux glibc,
                    "/usr/lib/libSystem.dylib" // OSX POSIX
                }
            });
        }

#pragma warning disable 649 // Set by MonoMod
        [DynDllImport("mmap")]
        public static mmapDelegate mmap;

        [DynDllImport("munmap")]
        public static munmapDelegate munmap;
#pragma warning restore 649
    }
}
