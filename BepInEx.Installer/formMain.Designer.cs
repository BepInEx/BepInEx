namespace BepInEx.Installer
{
	partial class formMain
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(formMain));
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.label1 = new System.Windows.Forms.Label();
			this.btnBrowseTarget = new System.Windows.Forms.Button();
			this.label2 = new System.Windows.Forms.Label();
			this.txtTargetGamePath = new System.Windows.Forms.TextBox();
			this.labelIsUnityGame = new System.Windows.Forms.Label();
			this.labelExeType = new System.Windows.Forms.Label();
			this.labelPlatform = new System.Windows.Forms.Label();
			this.btnPatch = new System.Windows.Forms.Button();
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.groupBox1.SuspendLayout();
			this.SuspendLayout();
			// 
			// tabControl1
			// 
			this.tabControl1.Controls.Add(this.tabPage1);
			this.tabControl1.Controls.Add(this.tabPage2);
			this.tabControl1.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.tabControl1.Enabled = false;
			this.tabControl1.Location = new System.Drawing.Point(0, 136);
			this.tabControl1.Name = "tabControl1";
			this.tabControl1.SelectedIndex = 0;
			this.tabControl1.Size = new System.Drawing.Size(605, 297);
			this.tabControl1.TabIndex = 0;
			// 
			// tabPage1
			// 
			this.tabPage1.Controls.Add(this.btnPatch);
			this.tabPage1.Location = new System.Drawing.Point(4, 22);
			this.tabPage1.Name = "tabPage1";
			this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage1.Size = new System.Drawing.Size(597, 271);
			this.tabPage1.TabIndex = 0;
			this.tabPage1.Text = "Basic Installation";
			this.tabPage1.UseVisualStyleBackColor = true;
			// 
			// tabPage2
			// 
			this.tabPage2.Location = new System.Drawing.Point(4, 22);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(597, 271);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "Advanced";
			this.tabPage2.UseVisualStyleBackColor = true;
			// 
			// groupBox1
			// 
			this.groupBox1.Controls.Add(this.labelPlatform);
			this.groupBox1.Controls.Add(this.labelExeType);
			this.groupBox1.Controls.Add(this.labelIsUnityGame);
			this.groupBox1.Controls.Add(this.txtTargetGamePath);
			this.groupBox1.Controls.Add(this.label2);
			this.groupBox1.Controls.Add(this.btnBrowseTarget);
			this.groupBox1.Location = new System.Drawing.Point(12, 33);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(581, 97);
			this.groupBox1.TabIndex = 1;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Target Game";
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(12, 9);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(74, 13);
			this.label1.TabIndex = 2;
			this.label1.Text = "BepInEx 5.0.0";
			// 
			// btnBrowseTarget
			// 
			this.btnBrowseTarget.Location = new System.Drawing.Point(6, 19);
			this.btnBrowseTarget.Name = "btnBrowseTarget";
			this.btnBrowseTarget.Size = new System.Drawing.Size(75, 23);
			this.btnBrowseTarget.TabIndex = 0;
			this.btnBrowseTarget.Text = "Browse...";
			this.btnBrowseTarget.UseVisualStyleBackColor = true;
			this.btnBrowseTarget.Click += new System.EventHandler(this.btnBrowseTarget_Click);
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(6, 50);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(108, 39);
			this.label2.TabIndex = 1;
			this.label2.Text = "Unity engine revision:\r\nExecutable type:\r\nCurrent platform:";
			this.label2.TextAlign = System.Drawing.ContentAlignment.TopRight;
			// 
			// txtTargetGamePath
			// 
			this.txtTargetGamePath.Location = new System.Drawing.Point(87, 21);
			this.txtTargetGamePath.Name = "txtTargetGamePath";
			this.txtTargetGamePath.ReadOnly = true;
			this.txtTargetGamePath.Size = new System.Drawing.Size(488, 20);
			this.txtTargetGamePath.TabIndex = 2;
			// 
			// labelIsUnityGame
			// 
			this.labelIsUnityGame.AutoSize = true;
			this.labelIsUnityGame.Location = new System.Drawing.Point(111, 50);
			this.labelIsUnityGame.Name = "labelIsUnityGame";
			this.labelIsUnityGame.Size = new System.Drawing.Size(53, 13);
			this.labelIsUnityGame.TabIndex = 3;
			this.labelIsUnityGame.Text = "Unknown";
			// 
			// labelExeType
			// 
			this.labelExeType.AutoSize = true;
			this.labelExeType.Location = new System.Drawing.Point(111, 63);
			this.labelExeType.Name = "labelExeType";
			this.labelExeType.Size = new System.Drawing.Size(53, 13);
			this.labelExeType.TabIndex = 4;
			this.labelExeType.Text = "Unknown";
			// 
			// labelPlatform
			// 
			this.labelPlatform.AutoSize = true;
			this.labelPlatform.Location = new System.Drawing.Point(111, 76);
			this.labelPlatform.Name = "labelPlatform";
			this.labelPlatform.Size = new System.Drawing.Size(53, 13);
			this.labelPlatform.TabIndex = 5;
			this.labelPlatform.Text = "Unknown";
			// 
			// btnPatch
			// 
			this.btnPatch.Location = new System.Drawing.Point(6, 6);
			this.btnPatch.Name = "btnPatch";
			this.btnPatch.Size = new System.Drawing.Size(585, 23);
			this.btnPatch.TabIndex = 0;
			this.btnPatch.Text = "Patch";
			this.btnPatch.UseVisualStyleBackColor = true;
			// 
			// formMain
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.BackColor = System.Drawing.SystemColors.Menu;
			this.ClientSize = new System.Drawing.Size(605, 433);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.groupBox1);
			this.Controls.Add(this.tabControl1);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MaximizeBox = false;
			this.Name = "formMain";
			this.Text = "BepInEx Installer";
			this.tabControl1.ResumeLayout(false);
			this.tabPage1.ResumeLayout(false);
			this.groupBox1.ResumeLayout(false);
			this.groupBox1.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.TabControl tabControl1;
		private System.Windows.Forms.TabPage tabPage1;
		private System.Windows.Forms.Button btnPatch;
		private System.Windows.Forms.TabPage tabPage2;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.Label labelPlatform;
		private System.Windows.Forms.Label labelExeType;
		private System.Windows.Forms.Label labelIsUnityGame;
		private System.Windows.Forms.TextBox txtTargetGamePath;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Button btnBrowseTarget;
		private System.Windows.Forms.Label label1;
	}
}

