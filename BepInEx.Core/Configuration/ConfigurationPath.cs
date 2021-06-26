using System;
using System.Collections.Generic;
using System.Linq;

namespace BepInEx.Configuration
{
    public static class ConfigurationPath
    {
        public static readonly string KeyDelimiter = ":";

        public static string Combine(IEnumerable<string> segements)
        {
            segements = segements ?? throw new ArgumentNullException(nameof(segements));
            return string.Join(KeyDelimiter, segements.ToArray());
        }
    }
}
