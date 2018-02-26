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
        private static Dictionary<string, Dictionary<string, string>> cache = new Dictionary<string, Dictionary<string, string>>();

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

            string currentSection = "";

            foreach (string rawLine in File.ReadAllLines(configPath))
            {
                string line = rawLine.Trim();

                int commentIndex = line.IndexOf("//");

                if (commentIndex != -1) //trim comment
                    line = line.Remove(commentIndex);

                if (line.StartsWith("[") && line.EndsWith("]")) //section
                {
                    currentSection = line.Substring(1, line.Length - 2);
                    continue;
                }

                string[] split = line.Split('='); //actual config line
                if (split.Length != 2)
                    continue; //empty/invalid line

                if (!cache.ContainsKey(currentSection))
                    cache[currentSection] = new Dictionary<string, string>();

                cache[currentSection][split[0]] = split[1];
            }
        }

        /// <summary>
        /// Writes the config to disk.
        /// </summary>
        public static void SaveConfig()
        {
            using (StreamWriter writer = new StreamWriter(File.Create(configPath), System.Text.Encoding.UTF8))
                foreach (var sectionKv in cache)
                {
                    writer.WriteLine($"[{sectionKv.Key}]");

                    foreach (var entryKv in sectionKv.Value)
                        writer.WriteLine($"{entryKv.Key}={entryKv.Value}");

                    writer.WriteLine();
                }
        }

        /// <summary>
        /// Returns the value of the key if found, otherwise returns the default value.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <param name="defaultValue">The default value to return if the key is not found.</param>
        /// <returns>The value of the key.</returns>
        public static string GetEntry(string key, string defaultValue = "", string section = "")
        {
            if (section.IsNullOrWhiteSpace())
                section = "Global";

            Dictionary<string, string> subdict;

            if (!cache.TryGetValue(section, out subdict))
                return defaultValue;
                

            if (subdict.TryGetValue(key, out string value))
                return value;
            else
                return defaultValue;
        }

        /// <summary>
        /// Sets the value of the key in the config.
        /// </summary>
        /// <param name="key">The key to set the value to.</param>
        /// <param name="value">The value to set.</param>
        public static void SetEntry(string key, string value, string section = "")
        {
            if (section.IsNullOrWhiteSpace())
                section = "Global";

            Dictionary<string, string> subdict;

            if (!cache.TryGetValue(section, out subdict))
            {
                subdict = new Dictionary<string, string>();
                cache[section] = subdict;
            }

            subdict[key] = value;

            if (SaveOnConfigSet)
                SaveConfig();
        }

        #region Extensions
        public static string GetEntry(this BaseUnityPlugin plugin, string key, string defaultValue = "")
        {
            return GetEntry(key, defaultValue, plugin.ID);
        }

        public static void SetEntry(this BaseUnityPlugin plugin, string key, string value)
        {
            SetEntry(key, value, plugin.ID);
        }
        #endregion
    }
}
