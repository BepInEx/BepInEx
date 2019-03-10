using System;
using System.Drawing;
using System.Windows.Forms;
using BepInEx.Installer.Patching;

namespace BepInEx.Installer
{
	public partial class formMain : Form
	{
		public formMain()
		{
			InitializeComponent();
		}

		public bool IsValid { get; protected set; }

		public string SelectedPath { get; protected set; }

		public TargetGame TargetGame { get; protected set; }

		private void btnBrowseTarget_Click(object sender, EventArgs e)
		{
			using (var directoryBrowser = new FolderBrowserDialog())
			{
				if (directoryBrowser.ShowDialog() != DialogResult.OK)
					return;

				SelectedPath = directoryBrowser.SelectedPath;

				txtTargetGamePath.Text = SelectedPath;
				TargetGame = new TargetGame(SelectedPath);


				SetDefaults();
				UpdateLabels();
			}
		}

		private void SetDefaults()
		{
			checkRuntimePatches.Checked = true;

			cmbTargetAssembly.Text = TargetGame.UnityEngineType == TargetGame.UnityGameType.v2017Plus
				? "UnityEngine.CoreModule.dll"
				: "UnityEngine.dll";

			cmbTargetClass.Text = "Application";
			cmbTargetMethod.Text = ".cctor";
		}

		private void UpdateLabels()
		{
			IsValid = true;

			labelIsUnityGame.Text = TargetGame.UnityEngineType.ToString();

			if (TargetGame.UnityEngineType == TargetGame.UnityGameType.Legacy)
			{
				labelIsUnityGame.ForeColor = Color.Green;
			}
			else if (TargetGame.UnityEngineType == TargetGame.UnityGameType.v2017Plus)
			{
				labelIsUnityGame.ForeColor = Color.Green;
				labelIsUnityGame.Text = "v2017+";
			}
			else if (TargetGame.UnityEngineType == TargetGame.UnityGameType.Unknown)
			{
				labelIsUnityGame.ForeColor = Color.Red;
				IsValid = false;
			}
			else if (TargetGame.UnityEngineType == TargetGame.UnityGameType.UnknownMultiple)
			{
				labelIsUnityGame.ForeColor = Color.Red;
				labelIsUnityGame.Text = "Unknown (multiple games detected)";
				IsValid = false;
			}


			labelExeType.Text = TargetGame.Platform.ToString();

			if (TargetGame.Platform == TargetGame.ExecutablePlatform.Unknown)
			{
				labelExeType.ForeColor = Color.Red;
				IsValid = false;
			}
			else if (TargetGame.Platform == TargetGame.ExecutablePlatform.Linux
					 || TargetGame.Platform == TargetGame.ExecutablePlatform.Mac)
			{
				labelExeType.ForeColor = Color.Goldenrod;

				radioDoorstop.Checked = false;
				radioDoorstop.Enabled = false;
				radioPatch.Enabled = true;
			}
			else
			{
				labelExeType.ForeColor = Color.Green;

				radioDoorstop.Checked = true;
				radioDoorstop.Enabled = true;
			}

			tabControl1.Enabled = IsValid;
		}

		private Config GenerateAdvancedConfig()
		{
			var runtimePatchesType = checkRuntimePatches.Checked
				? RuntimePatchesType.EnabledHarmony
				: RuntimePatchesType.Disabled;

			return new Config(cmbTargetAssembly.Text, cmbTargetClass.Text, cmbTargetMethod.Text, runtimePatchesType);
		}

		private InstallationType InstallationType
		{
			get
			{
				if (radioDoorstop.Checked)
					return InstallationType.Doorstop;

				if (radioCryptoRng.Checked)
					return InstallationType.CryptoRng;

				if (radioPatch.Checked)
					return InstallationType.AssemblyPatch;

				throw new InvalidOperationException();
			}
		}

		private void btnAdvancedPatch_Click(object sender, EventArgs e)
		{
			var installer = new FrameworkInstaller(TargetGame, InstallationType, GenerateAdvancedConfig());

			installer.Install();
		}
	}
}