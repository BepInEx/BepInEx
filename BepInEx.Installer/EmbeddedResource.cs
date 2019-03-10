using System;
using System.IO;
using System.Reflection;

namespace BepInEx.Installer
{
	internal static class EmbeddedResource
	{
		public static Stream GetStream(string resourceName)
		{
			Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);

			if (stream == null)
				throw new ArgumentException("Unable to find resource", nameof(resourceName));

			return stream;
		}

		public static byte[] GetBytes(string resourceName)
		{
			using (Stream stream = GetStream(resourceName))
			{
				int length = (int)stream.Length;

				byte[] buffer = new byte[length];

				stream.Read(buffer, 0, length);

				return buffer;
			}
		}
	}
}