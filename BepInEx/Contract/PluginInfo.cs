using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Bootstrap;

namespace BepInEx.Contract
{
	public class PluginInfo : ICacheable
	{
		public BepInPlugin Metadata { get; internal set; }

		public IEnumerable<BepInProcess> Processes { get; internal set; }

		public IEnumerable<BepInDependency> Dependencies { get; internal set; }

		public string Location { get; internal set; }

		public BaseUnityPlugin Instance { get; internal set; }

		internal string TypeName { get; set; }

		public void Save(BinaryWriter bw)
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
			{
				bw.Write(bepInDependency.DependencyGUID);
				bw.Write((int)bepInDependency.Flags);
				bw.Write(bepInDependency.MinimumVersion.ToString());
			}
		}

		public void Load(BinaryReader br)
		{
			TypeName = br.ReadString();

			Metadata = new BepInPlugin(br.ReadString(), br.ReadString(), br.ReadString());

			var processListCount = br.ReadInt32();
			var processList = new List<BepInProcess>(processListCount);
			for (int i = 0; i < processListCount; i++)
				processList.Add(new BepInProcess(br.ReadString()));
			Processes = processList;

			var depCount = br.ReadInt32();
			var depList = new List<BepInDependency>(depCount);
			for (int i = 0; i < depCount; i++)
				depList.Add(new BepInDependency(br.ReadString(), (BepInDependency.DependencyFlags) br.ReadInt32(), br.ReadString()));
			Dependencies = depList;
		}
	}
}