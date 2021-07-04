using System;
using System.Collections.Generic;

namespace BepInEx.Configuration
{
    public interface IConfigurationProvider
    {
        void Load();

        void Save();

        ConfigurationNode Get(string[] path);

        void Set(string[] path, ConfigurationNode node);

        ConfigurationNode Delete(string[] path);
        
        IEnumerable<string[]> EntryPaths { get; }
    }

    public class ConfigurationNode
    {
        public string Value { get; set; }
        public string Comment { get; set; }
        public Type ValueType { get; set; }
    }
}
