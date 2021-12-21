using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MonoMod.Utils;

namespace BepInEx.IL2CPP.Hook.Allocator;

internal class PageAllocatorException : Exception
{
    public PageAllocatorException(string message) : base(message) { }
}

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

    private readonly List<PageChunk> allocatedChunks = new();

    /// <summary>
    ///     Platform-specific instance of page allocator.
    /// </summary>
    public static PageAllocator Instance => instance ??= Init();

    /// <summary>
    ///     Allocates a single 64k chunk of memory near the given address
    /// </summary>
    /// <param name="hint">Address near which to attempt allocate the chunk</param>
    /// <returns>Allocated chunk</returns>
    /// <exception cref="PageAllocatorException">Allocation failed</exception>
    protected abstract nint AllocateChunk(nint hint);

    /// <summary>
    ///     Allocates a single page of size <see cref="PAGE_SIZE" /> near the provided address.
    ///     Attempts to allocate the page within the +-1GB region of the hinted address.
    /// </summary>
    /// <param name="hint">Address near which to attempt to allocate the page.</param>
    /// <returns>Address to the allocated page.</returns>
    public virtual nint Allocate(nint hint)
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

        var chunk = new PageChunk
        {
            BaseAddress = AllocateChunk(hint)
        };
        allocatedChunks.Add(chunk);
        chunk.Pages[0] = true;
        chunk.UsedPages++;
        return chunk.BaseAddress;
    }

    /// <summary>
    ///     Frees the page allocated with <see cref="Allocate" />
    /// </summary>
    /// <param name="page"></param>
    public void Free(nint page)
    {
        foreach (var allocatedChunk in allocatedChunks)
        {
            long index = (page - allocatedChunk.BaseAddress) / PAGE_SIZE;
            if (index < 0 || index >= PAGES_PER_UNIT)
                continue;
            if (!allocatedChunk.Pages[index])
                return;
            allocatedChunk.Pages[index] = false;
            allocatedChunk.UsedPages--;
            return;
        }
    }

    /// <summary>
    ///     Checks if the given address is within the relative jump range.
    /// </summary>
    /// <param name="src">Source address to jump from.</param>
    /// <param name="dst">Destination address to jump to.</param>
    /// <returns>True, if the distance between the addresses is within the relative jump range (usually 1GB), otherwise false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInRelJmpRange(nint src, nint dst) => dst - src is <= int.MaxValue and >= int.MinValue;

    private static PageAllocator Init() =>
        PlatformHelper.Current switch
        {
            var v when v.Is(Platform.Windows) => new WindowsPageAllocator(),
            var v when v.Is(Platform.Linux)   => new LinuxPageAllocator(),
            var v when v.Is(Platform.MacOS)   => new MacOsPageAllocator(),
            _                                 => throw new NotSupportedException()
        };

    private class PageChunk
    {
        public readonly bool[] Pages = new bool[PAGES_PER_UNIT];
        public nint BaseAddress;
        public int UsedPages;

        public nint GetPage(int index) => BaseAddress + index * PAGE_SIZE;
    }
}

internal static class PlatformExt
{
    public static bool Is(this Platform pl, Platform val) => (pl & val) == val;
}
