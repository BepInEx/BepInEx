using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Bootstrap;

namespace BepInEx
{
	public class PluginInfo : ICacheable
	{
		public BepInPlugin Metadata { get; internal set; }

		public IEnumerable<BepInProcess> Processes { get; internal set; }

		public IEnumerable<BepInDependency> Dependencies { get; internal set; }

		public IEnumerable<BepInIncompatibility> Incompatibilities { get; internal set; }

		public string Location { get; internal set; }

		public object Instance { get; internal set; }

		public string TypeName { get; internal set; }

		internal Version TargettedBepInExVersion { get; set; }

		void ICacheable.Save(BinaryWriter bw)
		{
			bw.Write(TypeName);
			bw.Write(Location);

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

			bw.Write(TargettedBepInExVersion.ToString(4));
		}

		void ICacheable.Load(BinaryReader br)
		{
			TypeName = br.ReadString();
			Location = br.ReadString();

			Metadata = new BepInPlugin(br.ReadString(), br.ReadString(), br.ReadString());

			var processListCount = br.ReadInt32();
			var processList = new List<BepInProcess>(processListCount);
			for (int i = 0; i < processListCount; i++)
				processList.Add(new BepInProcess(br.ReadString()));
			Processes = processList;

			var depCount = br.ReadInt32();
			var depList = new List<BepInDependency>(depCount);
			for (int i = 0; i < depCount; i++)
			{
				var dep = new BepInDependency("");
				((ICacheable)dep).Load(br);
				depList.Add(dep);
			}

			Dependencies = depList;

			var incCount = br.ReadInt32();
			var incList = new List<BepInIncompatibility>(incCount);
			for (int i = 0; i < incCount; i++)
			{
				var inc = new BepInIncompatibility("");
				((ICacheable)inc).Load(br);
				incList.Add(inc);
			}

			Incompatibilities = incList;

			TargettedBepInExVersion = new Version(br.ReadString());
		}
	}
}
