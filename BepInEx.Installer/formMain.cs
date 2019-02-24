using System;
using System.Drawing;
using System.Windows.Forms;

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

				UpdateLabels();
			}
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
			}
			else
			{
				labelExeType.ForeColor = Color.Green;
			}

			tabControl1.Enabled = IsValid;
		}
	}
}