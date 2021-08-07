using System;
using System.Collections.Generic;

namespace BepInEx.Configuration.Tests
{
    public class LegacyTestConfigProvider : LegacyConfigurationProvider
    {
        private string fileContents;

        public string FileContents
        {
            get => fileContents;
            set => fileContents = value.Replace("\r\n", "\n");
         }

        public LegacyTestConfigProvider() : base("none")
        {
        }

        public override IEnumerable<string> ReadAllLines()
        {
            return FileContents.Split("\n");
        }

        public override void Save()
        {
            // Do nothing for now, there is nothing to save
        }

        public string GetRawValue(string[] path)
        {
            if (!items.TryGetValue(path, out var value))
                return null;
            return value;
        }
        
        public static (ConfigFile Config, LegacyTestConfigProvider Provider) MakeConfig(string contents = "")
        {
            var provider = new LegacyTestConfigProvider{ FileContents = contents };
            return (new ConfigFile(provider, false, null), provider);
        }
    }
}
