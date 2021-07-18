using System;
using System.Collections.Generic;

namespace BepInEx.Configuration.Tests
{
    public class LegacyTestConfigProvider : LegacyConfigurationProvider
    {
        public string FileContents { get; set; }

        public LegacyTestConfigProvider() : base("none")
        {
        }

        public override IEnumerable<string> ReadAllLines()
        {
            return FileContents.Split(Environment.NewLine);
        }

        public override void Save()
        {
            // Do nothing for now, there is nothing to save
        }
        
        public static (ConfigFile Config, LegacyTestConfigProvider Provider) MakeConfig(string contents = "")
        {
            var provider = new LegacyTestConfigProvider{ FileContents = contents };
            return (new ConfigFile(provider, false, null), provider);
        }
    }
}
