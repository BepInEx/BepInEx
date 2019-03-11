using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Ionic.Zip;
using Mono.Cecil;
using Mono.Cecil.Cil;

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
			try
			{
				switch (InstallationType)
				{
					case InstallationType.Doorstop:
						DoorstopInstallation();
						break;
					case InstallationType.CryptoRng:
						throw new NotImplementedException();
					case InstallationType.AssemblyPatch:
						AssemblyPatchInstallation();
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(InstallationType));
				}

				MessageBox.Show("Installation completed successfully.", "Installation", MessageBoxButtons.OK, MessageBoxIcon.None);
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Error during installation", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private static ZipFile GetPackageZip()
		{
			return ZipFile.Read(EmbeddedResource.GetStream("BepInEx.Installer.InstallPackage.zip"));
		}

		#region Doorstop

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
				using (var doorstopZip = ZipFile.Read(packageZip[doorstopZipName].ExtractEntry()))
				{
					doorstopZip["winhttp.dll"].Extract(TargetGame.DirectoryPath, ExtractExistingFileAction.OverwriteSilently);
				}

				var selectedEntries = packageZip.SelectEntries("*", "shared")
												.Concat(packageZip.SelectEntries("*", coreAssemblyZipFolder));

				foreach (var entry in selectedEntries)
				{
					string localFilename = Path.Combine(coreDirectory, Path.GetFileName(entry.FileName));

					entry.ExtractEntry(localFilename);
				}
			}

			File.WriteAllText(Path.Combine(bepinexDirectory, "config.ini"), Config.OutputConfig);

			File.WriteAllText(Path.Combine(TargetGame.DirectoryPath, "doorstop_config.ini"), DoorstopConfig);
		}

		#endregion

		#region Assembly Patch

		private void AssemblyPatchInstallation()
		{
			string bepinexDirectory = Path.Combine(TargetGame.DirectoryPath, "BepInEx");
			string coreDirectory = Path.Combine(bepinexDirectory, "core");

			string managedDir = Path.Combine(TargetGame.DataFolder, "Managed");
			string unityEngineDllPath = Path.Combine(managedDir, Config.TargetAssembly);
			string bootstrapDllPath = Path.Combine(managedDir, "BepInEx.Bootstrap.dll");

			if (!File.Exists(unityEngineDllPath))
				throw new ArgumentException($"Cannot find target assembly '{unityEngineDllPath}'");

			Directory.CreateDirectory(coreDirectory);

			string coreAssemblyZipFolder = TargetGame.UnityEngineType == TargetGame.UnityGameType.v2017Plus
				? "v2018"
				: "legacy";

			using (var packageZip = GetPackageZip())
			{
				var selectedEntries = packageZip.SelectEntries("*", "shared")
												.Concat(packageZip.SelectEntries("*", coreAssemblyZipFolder))
												.Where(x => !x.FileName.EndsWith("BepInEx.Preloader.dll"));

				foreach (var entry in selectedEntries)
				{
					string localFilename = Path.Combine(coreDirectory, Path.GetFileName(entry.FileName));

					entry.ExtractEntry(localFilename);
				}

				packageZip["bootstrap\\BepInEx.Bootstrap.dll"].ExtractEntry(bootstrapDllPath);
			}

			if (!PatchUnityGame(managedDir, unityEngineDllPath, bootstrapDllPath, out var message))
				throw new Exception(message);
		}

		bool PatchUnityGame(string managedDir, string unityEngineDll, string bootstrapDll, out string message)
		{
			var defaultResolver = new DefaultAssemblyResolver();
			defaultResolver.AddSearchDirectory(managedDir);
			var rp = new ReaderParameters
			{
				AssemblyResolver = defaultResolver
			};
			
			string unityBackupDll = Path.GetFullPath($"{unityEngineDll}.bak");

			//determine which assembly to use as a base
			AssemblyDefinition unity = AssemblyDefinition.ReadAssembly(unityEngineDll, rp);

			if (!VerifyAssembly(unity))
			{
				//try and fall back to .bak if exists
				if (File.Exists(unityBackupDll))
				{
					unity.Dispose();
					unity = AssemblyDefinition.ReadAssembly(unityBackupDll, rp);

					if (!VerifyAssembly(unity))
					{
						//can't use anything
						unity.Dispose();
						message = "Target assembly already has BepInEx injected, and the backup is not usable.";
						return false;
					}
				}
				else
				{
					//can't use anything
					unity.Dispose();
					message = "Target assembly already has BepInEx injected, and no backup exists.";
					return false;
				}
			}
			else
			{
				//make a backup of the assembly
				File.Copy(unityEngineDll, unityBackupDll, true);
				unity.Dispose();
				unity = AssemblyDefinition.ReadAssembly(unityBackupDll, rp);
			}

			//patch
			using (unity)
			using (AssemblyDefinition injected = AssemblyDefinition.ReadAssembly(bootstrapDll, rp))
			{
				InjectAssembly(unity, injected);

				unity.Write(unityEngineDll);
			}

			message = null;
			return true;
		}

		void InjectAssembly(AssemblyDefinition targetAssembly, AssemblyDefinition injected)
		{
			//Entry point
			var originalInjectMethod = injected.MainModule.Types.First(x => x.Name == "Entrypoint")
											   .Methods.First(x => x.Name == "Init");

			var injectMethod = targetAssembly.MainModule.ImportReference(originalInjectMethod);

			var targetType = targetAssembly.MainModule.Types.FirstOrDefault(x => x.Name == Config.TargetType);

			if (targetType == null)
				throw new ArgumentException($"Cannot find target type '{Config.TargetType}'");

			MethodDefinition targetMethod;

			if (Config.TargetMethod == ".cctor")
			{
				targetMethod = targetType.Methods.FirstOrDefault(m => m.IsConstructor && m.IsStatic);

				if (targetMethod == null)
				{
					targetMethod = new MethodDefinition(".cctor",
						MethodAttributes.Static
						| MethodAttributes.Private
						| MethodAttributes.HideBySig
						| MethodAttributes.SpecialName
						| MethodAttributes.RTSpecialName,
						targetAssembly.MainModule.ImportReference(typeof(void)));

					targetType.Methods.Add(targetMethod);
					var il = targetMethod.Body.GetILProcessor();
					il.Append(il.Create(OpCodes.Ret));
				}
			}
			else
			{
				targetMethod = targetType.Methods.FirstOrDefault(m => m.Name == Config.TargetMethod);

				if (targetMethod == null)
					throw new ArgumentException($"Cannot find target method '{Config.TargetType}.{Config.TargetMethod}'");
			}

			var ilp = targetMethod.Body.GetILProcessor();
			ilp.InsertBefore(ilp.Body.Instructions.First(), ilp.Create(OpCodes.Call, injectMethod));
		}

		static bool VerifyAssembly(AssemblyDefinition unity)
			=> !unity.MainModule.AssemblyReferences.Any(x => x.Name.Contains("BepInEx"));

		#endregion
	}
}