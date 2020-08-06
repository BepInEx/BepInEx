using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Bootstrap;

namespace BepInEx
{
	/// <summary>
	/// Data class that represents information about a loadable BepInEx plugin.
	/// Contains all metadata and additional info required for plugin loading by <see cref="Chainloader"/>.
	/// </summary>
	public class PluginInfo : ICacheable
	{
		/// <summary>
		/// General metadata about a plugin.
		/// </summary>
		public BepInPlugin Metadata { get; internal set; }

		/// <summary>
		/// Collection of <see cref="BepInProcess"/> attributes that describe what processes the plugin can run on.
		/// </summary>
		public IEnumerable<BepInProcess> Processes { get; internal set; }

		/// <summary>
		/// Collection of <see cref="BepInDependency"/> attributes that describe what plugins this plugin depends on.
		/// </summary>
		public IEnumerable<BepInDependency> Dependencies { get; internal set; }

		/// <summary>
		/// Collection of <see cref="BepInIncompatibility"/> attributes that describe what plugins this plugin
		/// is incompatible with.
		/// </summary>
		public IEnumerable<BepInIncompatibility> Incompatibilities { get; internal set; }

		/// <summary>
		/// File path to the plugin DLL
		/// </summary>
		public string Location { get; internal set; }

		/// <summary>
		/// Instance of the plugin that represents this info. NULL if no plugin is instantiated from info (yet)
		/// </summary>
		public BaseUnityPlugin Instance { get; internal set; }

		internal string TypeName { get; set; }

		internal Version TargettedBepInExVersion { get; set; }

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

			bw.Write(TargettedBepInExVersion.ToString(4));
		}

		void ICacheable.Load(BinaryReader br)
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

		/// <inheritdoc />
		public override string ToString() => $"{Metadata?.Name} {Metadata?.Version}";
	}
}
