using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace BepInEx.Patcher
{
    internal static class EmbeddedResource
    {
        public static byte[] Get(string resourceName)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                int length = (int)stream.Length;

                byte[] buffer = new byte[length];

                stream.Read(buffer, 0, length);

                return buffer;
            }
        }
    }
}
