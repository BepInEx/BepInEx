using System;
using System.IO;

namespace BepInEx.Installer
{
	public class TargetGame
	{
		public string DirectoryPath { get; protected set; }

		public string DataFolder { get; protected set; }

		public UnityGameType UnityEngineType { get; protected set; }
		public ExecutablePlatform Platform { get; protected set; }

		public TargetGame(string directoryPath)
		{
			DirectoryPath = directoryPath;

			UnityEngineType = DetectGameType();
			Platform = DetectExecutablePlatform();
		}

		protected virtual UnityGameType DetectGameType()
		{
			if (string.IsNullOrEmpty(DirectoryPath) || !Directory.Exists(DirectoryPath))
				return UnityGameType.Unknown;

			var dataDirectories = Directory.GetDirectories(DirectoryPath, "*_Data", SearchOption.TopDirectoryOnly);

			if (dataDirectories.Length > 1)
				return UnityGameType.UnknownMultiple;

			if (dataDirectories.Length == 0)
				return UnityGameType.Unknown;

			DataFolder = dataDirectories[0];
			string managedFolder = Path.Combine(DataFolder, "Managed");

			if (!Directory.Exists(managedFolder))
				return UnityGameType.Unknown;

			if (File.Exists(Path.Combine(managedFolder, "UnityEngine.CoreModule.dll")))
				return UnityGameType.v2017Plus;

			if (File.Exists(Path.Combine(managedFolder, "UnityEngine.dll")))
				return UnityGameType.Legacy;

			return UnityGameType.Unknown;
		}

		protected virtual ExecutablePlatform DetectExecutablePlatform()
		{
			if (string.IsNullOrEmpty(DirectoryPath) || !Directory.Exists(DirectoryPath)
				|| string.IsNullOrEmpty(DataFolder) || !Directory.Exists(DataFolder))
				return ExecutablePlatform.Unknown;

			if (Environment.OSVersion.Platform == PlatformID.Unix)
				return ExecutablePlatform.Linux;

			if (Environment.OSVersion.Platform == PlatformID.MacOSX)
				return ExecutablePlatform.Mac;

			string exeName = Path.Combine(DirectoryPath, DataFolder.Substring(0, DataFolder.Length - 5) + ".exe");

			if (!File.Exists(exeName))
				return ExecutablePlatform.Unknown;

			var machineType = PEAnalyzer.GetMachineType(exeName);

			if (machineType == PEAnalyzer.MachineType.I386)
				return ExecutablePlatform.Win32;

			if (machineType == PEAnalyzer.MachineType.x64)
				return ExecutablePlatform.Win64;
			
			return ExecutablePlatform.Unknown;
		}

		public enum UnityGameType
		{
			Unknown,
			UnknownMultiple,
			Legacy,
			v2017Plus
		}

		public enum ExecutablePlatform
		{
			Unknown,
			Win32,
			Win64,
			Linux,
			Mac
		}
	}
}