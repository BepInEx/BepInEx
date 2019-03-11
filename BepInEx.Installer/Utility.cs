using System.IO;
using System.Linq;
using Ionic.Zip;

namespace BepInEx.Installer
{
	internal static class Utility
	{
		public static string CombinePaths(params string[] pathSegments)
			=> pathSegments.Aggregate(Path.Combine);

		public static MemoryStream ExtractEntry(this ZipEntry entry)
		{
			MemoryStream ms = new MemoryStream((int)entry.UncompressedSize);

			entry.Extract(ms);

			ms.Position = 0;

			return ms;
		}

		public static void ExtractEntry(this ZipEntry entry, string filename)
		{
			using (FileStream fs = new FileStream(filename, FileMode.Create))
				entry.Extract(fs);
		}
	}
}
