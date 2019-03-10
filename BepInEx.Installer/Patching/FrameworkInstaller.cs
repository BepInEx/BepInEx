using System;
using System.IO;
using System.Linq;
using Ionic.Zip;

namespace BepInEx.Installer.Patching
{
	public class FrameworkInstaller
	{
		public TargetGame TargetGame { get; }

		public InstallationType InstallationType { get; }

		public Config Config { get; }

		public FrameworkInstaller(TargetGame targetGame, InstallationType installationType, Config config)
		{
			TargetGame = targetGame;
			InstallationType = installationType;
			Config = config;
		}

		public void Install()
		{
			switch (InstallationType)
			{
				case InstallationType.Doorstop:
					DoorstopInstallation();
					break;
				case InstallationType.CryptoRng:
					throw new NotImplementedException();
				case InstallationType.AssemblyPatch:
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(InstallationType));
			}
		}

		private static ZipFile GetPackageZip()
		{
			return ZipFile.Read(EmbeddedResource.GetStream("BepInEx.Installer.InstallPackage.zip"));
		}

		private static MemoryStream ExtractZipEntry(ZipEntry entry)
		{
			MemoryStream ms = new MemoryStream((int)entry.UncompressedSize);

			entry.Extract(ms);

			ms.Position = 0;

			return ms;
		}

		private const string DoorstopConfig = @"[UnityDoorstop]
# Specifies whether assembly executing is enabled
enabled=true
# Specifies the path (absolute, or relative to the game's exe) to the DLL/EXE that should be executed by Doorstop
targetAssembly=BepInEx\core\BepInEx.Preloader.dll";

		private void DoorstopInstallation()
		{
			string bepinexDirectory = Path.Combine(TargetGame.DirectoryPath, "BepInEx");
			string coreDirectory = Path.Combine(bepinexDirectory, "core");

			Directory.CreateDirectory(coreDirectory);

			string doorstopZipName = TargetGame.Platform == TargetGame.ExecutablePlatform.Win64
				? "doorstop_x64.zip"
				: "doorstop_x86.zip";

			string coreAssemblyZipFolder = TargetGame.UnityEngineType == TargetGame.UnityGameType.v2017Plus
				? "v2018"
				: "legacy";

			using (var packageZip = GetPackageZip())
			{
				using (var doorstopZip = ZipFile.Read(ExtractZipEntry(packageZip[doorstopZipName])))
				{
					doorstopZip["winhttp.dll"].Extract(TargetGame.DirectoryPath, ExtractExistingFileAction.OverwriteSilently);
				}

				var selectedEntries = packageZip.SelectEntries("*", "shared")
												.Concat(packageZip.SelectEntries("*", coreAssemblyZipFolder));

				foreach (var entry in selectedEntries)
				{
					string localFilename = Path.Combine(coreDirectory, Path.GetFileName(entry.FileName));

					using (FileStream fs = new FileStream(localFilename, FileMode.Create))
						entry.Extract(fs);
				}
			}

			File.WriteAllText(Path.Combine(bepinexDirectory, "config.ini"), Config.OutputConfig);

			File.WriteAllText(Path.Combine(TargetGame.DirectoryPath, "doorstop_config.ini"), DoorstopConfig);
		}
	}
}