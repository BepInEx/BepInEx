using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace BepInEx.IL2CPP.Hook.Allocator;

internal class LinuxPageAllocator : UnixPageAllocator
{
    protected override IEnumerable<(nint, nint)> MapMemoryAreas()
    {
        // Each row of /proc/self/maps is as follows:
        // <start_address>-<end_address> <perms> <offset> <dev> <inode>		<owner_name>
        // More info: https://stackoverflow.com/a/1401595
        using var procMap = new StreamReader(File.OpenRead("/proc/self/maps"));

        string line;
        while ((line = procMap.ReadLine()) != null)
        {
            var startIndex = line.IndexOf('-');
            var endIndex = line.IndexOf(' ');
            var startAddr = long.Parse(line[..startIndex], NumberStyles.HexNumber);
            var endAddr = long.Parse(line[(startIndex + 1)..endIndex], NumberStyles.HexNumber);
            yield return ((nint) startAddr, (nint) endAddr);
        }
    }
}
