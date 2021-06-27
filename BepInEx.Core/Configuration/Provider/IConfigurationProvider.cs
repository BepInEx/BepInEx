using System;
using System.Collections.Generic;
using System.Linq;

namespace BepInEx.Configuration
{
    public interface IConfigurationProvider
    {
        public void Load();

        public void Save();

        public ConfigurationNode Get(string[] path);

        public void Set(string[] path, ConfigurationNode node);
        
        public IEnumerable<string[]> EntryPaths { get; }
    }

    public class ConfigurationNode
    {
        public string Value { get; set; }
        public string Comment { get; set; }
        public Type ValueType { get; set; }
    }
}
