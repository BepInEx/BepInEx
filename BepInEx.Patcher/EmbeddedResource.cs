using System.Reflection;

namespace BepInEx.Patcher
{
    internal static class EmbeddedResource
    {
        public static byte[] Get(string resourceName)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                var length = (int) stream.Length;

                var buffer = new byte[length];

                stream.Read(buffer, 0, length);

                return buffer;
            }
        }
    }
}
