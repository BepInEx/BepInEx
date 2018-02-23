using BepInEx.Common;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BepInEx
{
    /// <summary>
    /// A helper class to handle persistent data.
    /// </summary>
    public static class Config
    {
        private static Dictionary<string, string> cache = new Dictionary<string, string>();

        private static string configPath => Path.Combine(Utility.PluginsDirectory, "config.ini");

        /// <summary>
        /// If enabled, writes the config to disk every time a value is set.
        /// </summary>
        public static bool SaveOnConfigSet { get; set; } = true;

        static Config()
        {
            if (File.Exists(configPath))
            {
                ReloadConfig();
            }
            else
            {
                SaveConfig();
            }
        }

        /// <summary>
        /// Reloads the config from disk. Unwritten changes are lost.
        /// </summary>
        public static void ReloadConfig()
        {
            cache.Clear();

            foreach (string line in File.ReadAllLines(configPath))
            {
                string[] split = line.Split('=');
                if (split.Length != 2)
                    continue;

                cache[split[0]] = split[1];
            }
        }

        /// <summary>
        /// Writes the config to disk.
        /// </summary>
        public static void SaveConfig()
        {
            File.WriteAllLines(configPath, cache.Select(x => $"{x.Key}={x.Value}").ToArray());
        }

        /// <summary>
        /// Returns the value of the key if found, otherwise returns the default value.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <param name="defaultValue">The default value to return if the key is not found.</param>
        /// <returns>The value of the key.</returns>
        public static string GetEntry(string key, string defaultValue)
        {
            if (cache.TryGetValue(key, out string value))
                return value;
            else
                return defaultValue;
        }

        /// <summary>
        /// Sets the value of the key in the config.
        /// </summary>
        /// <param name="key">The key to set the value to.</param>
        /// <param name="value">The value to set.</param>
        public static void SetEntry(string key, string value)
        {
            cache[key] = value;

            if (SaveOnConfigSet)
                SaveConfig();
        }
    }
}
