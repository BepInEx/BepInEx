using BepInEx.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BepInEx
{
    public static class Config
    {
        private static Dictionary<string, string> cache = new Dictionary<string, string>();

        private static string configPath => Path.Combine(Utility.PluginsDirectory, "config.ini");

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

        public static void SaveConfig()
        {
            File.WriteAllLines(configPath, cache.Select(x => $"{x.Key}={x.Value}").ToArray());
        }

        public static string GetEntry(string key, string defaultValue)
        {
            if (cache.TryGetValue(key, out string value))
                return value;
            else
                return defaultValue;
        }

        public static void SetEntry(string key, string value)
        {
            cache[key] = value;

            if (SaveOnConfigSet)
                SaveConfig();
        }
    }
}
