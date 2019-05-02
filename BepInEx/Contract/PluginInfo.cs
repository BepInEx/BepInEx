using System.Collections.Generic;
using Mono.Cecil;

namespace BepInEx.Contract {
	public class PluginInfo
	{
		public BepInPlugin Metadata { get; internal set; }

		public IEnumerable<BepInProcess> Processes { get; internal set; }

		public IEnumerable<BepInDependency> Dependencies { get; internal set; }

		public string Location { get; internal set; }

		public BaseUnityPlugin Instance { get; internal set; }

		internal TypeDefinition CecilType { get; set; }
	}
}