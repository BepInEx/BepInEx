using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace BepInEx
{
    internal class ThunderstoreManifestV1
    {
        [JsonProperty("name")]
        internal string Name { get; set; }

        [JsonProperty("version_number")]
        internal string VersionNumber { get; set; }

        [JsonProperty("website_url")]
        internal Uri WebsiteUrl { get; set; }

        [JsonProperty("description")]
        internal string Description { get; set; }

        [JsonProperty("dependencies")]
        internal List<string> Dependencies { get; set; }
    }
}
