using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BepInEx.Configuration
{
    public class LegacyConfigurationProvider : IConfigurationProvider
    {
        private string filePath;

        private Dictionary<string[], string> items = new(new ArrayComparer());

        private class ArrayComparer : IEqualityComparer<string[]>
        {
            public bool Equals(string[] x, string[] y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (ReferenceEquals(x, null)) return false;
                if (ReferenceEquals(y, null)) return false;
                return x.GetType() == y.GetType() && x.SequenceEqual(y);
            }

            public int GetHashCode(string[] arr)
            {
                return arr.Aggregate(1009, (current, s) => current * 9176 + s.GetHashCode());
            }
        }
        
        public LegacyConfigurationProvider(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(filePath);
            this.filePath = filePath;
        }
        
        public void Load()
        {
            items.Clear();

            var currentSection = new string[0];
            
            foreach (var rawLine in File.ReadAllLines(filePath))
            {
                var line = rawLine.Trim();
                
                if (line.StartsWith("#")) //comment
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]")) //section
                {
                    currentSection = line[1..^2].Trim().Split('.');
                    continue;
                }

                var split = line.Split(new[] { '=' }, 2); //actual config line
                if (split.Length != 2)
                    continue; //empty/invalid line

                var currentKey = split[0].Trim();
                var currentValue = split[1].Trim();

                items[currentSection.Concat(new[] { currentKey }).ToArray()] = currentValue;
            }
        }

        // TODO: Write to TOML instead
        public void Save() => throw new System.NotImplementedException();

        public ConfigurationNode Get(string[] path)
        {
            if (!items.TryGetValue(path, out var value))
                return null;
            return new ConfigurationNode
            {
                ValueType = typeof(string),
                Value = value
            };
        }

        public void Set(string[] path, ConfigurationNode node) => items[path] = node.Value;

        public ConfigurationNode Delete(string[] path)
        {
            var val = Get(path);
            if (val == null) return null;
            items.Remove(path);
            return val;
        }

        public IEnumerable<string[]> EntryPaths => items.Keys.ToList();
    }
}
