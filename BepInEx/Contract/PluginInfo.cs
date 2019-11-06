using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Bootstrap;

namespace BepInEx
{
	public class PluginInfo : ICacheable
	{
		public PluginMetadata Metadata { get; internal set; }

		public IEnumerable<ProcessFilter> Processes { get; internal set; }

		public IEnumerable<PluginDependency> Dependencies { get; internal set; }

		public IEnumerable<PluginIncompatibility> Incompatibilities { get; internal set; }

		public string Location { get; internal set; }

		public BaseUnityPlugin Instance { get; internal set; }

		internal string TypeName { get; set; }

		void ICacheable.Save(BinaryWriter bw)
		{
			bw.Write(TypeName);

			bw.Write(Metadata.GUID);
			bw.Write(Metadata.Name);
			bw.Write(Metadata.Version.ToString());

			var processList = Processes.ToList();
			bw.Write(processList.Count);
			foreach (var bepInProcess in processList)
				bw.Write(bepInProcess.ProcessName);

			var depList = Dependencies.ToList();
			bw.Write(depList.Count);
			foreach (var bepInDependency in depList)
				((ICacheable)bepInDependency).Save(bw);

			var incList = Incompatibilities.ToList();
			bw.Write(incList.Count);
			foreach (var bepInIncompatibility in incList)
				((ICacheable)bepInIncompatibility).Save(bw);
		}

		void ICacheable.Load(BinaryReader br)
		{
			TypeName = br.ReadString();

			Metadata = new PluginMetadata(br.ReadString(), br.ReadString(), br.ReadString());

			var processListCount = br.ReadInt32();
			var processList = new List<ProcessFilter>(processListCount);
			for (int i = 0; i < processListCount; i++)
				processList.Add(new ProcessFilter(br.ReadString()));
			Processes = processList;

			var depCount = br.ReadInt32();
			var depList = new List<PluginDependency>(depCount);
			for (int i = 0; i < depCount; i++)
			{
				var dep = new PluginDependency("");
				((ICacheable)dep).Load(br);
				depList.Add(dep);
			}

			Dependencies = depList;

			var incCount = br.ReadInt32();
			var incList = new List<PluginIncompatibility>(incCount);
			for (int i = 0; i < incCount; i++)
			{
				var inc = new PluginIncompatibility("");
				((ICacheable)inc).Load(br);
				incList.Add(inc);
			}

			Incompatibilities = incList;
		}
	}
}
