using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MonoMod.Utils;

namespace BepInEx.IL2CPP.Allocator
{
	internal class LinuxPageAllocator : UnixPageAllocator
	{
		protected override IEnumerable<(IntPtr, IntPtr)> MapMemoryAreas()
		{
			// Each row of /proc/self/maps is as follows:
			// <start_address>-<end_address> <perms> <offset> <dev> <inode>		<owner_name>
			// More info: https://stackoverflow.com/a/1401595
			using var procMap = new StreamReader(File.OpenRead("/proc/self/maps"));

			string line;
			while ((line = procMap.ReadLine()) != null)
			{
				int startIndex = line.IndexOf('-');
				int endIndex = line.IndexOf(' ');
				long startAddr = long.Parse(line.Substring(0, startIndex), NumberStyles.HexNumber);
				long endAddr = long.Parse(line.Substring(startIndex + 1, endIndex - startIndex - 1), NumberStyles.HexNumber);
				yield return (new IntPtr(startAddr), new IntPtr(endAddr));
			}
		}
	}
}
