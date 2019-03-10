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
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(formMain));
			this.tabControl1 = new System.Windows.Forms.TabControl();
			this.tabPage1 = new System.Windows.Forms.TabPage();
			this.btnPatch = new System.Windows.Forms.Button();
			this.tabPage2 = new System.Windows.Forms.TabPage();
			this.button2 = new System.Windows.Forms.Button();
			this.groupBox3 = new System.Windows.Forms.GroupBox();
			this.cmbTargetMethod = new System.Windows.Forms.ComboBox();
			this.label5 = new System.Windows.Forms.Label();
			this.cmbTargetClass = new System.Windows.Forms.ComboBox();
			this.label4 = new System.Windows.Forms.Label();
			this.cmbTargetAssembly = new System.Windows.Forms.ComboBox();
			this.label3 = new System.Windows.Forms.Label();
			this.checkRuntimePatches = new System.Windows.Forms.CheckBox();
			this.btnAdvancedPatch = new System.Windows.Forms.Button();
			this.groupBox2 = new System.Windows.Forms.GroupBox();
			this.radioPatch = new System.Windows.Forms.RadioButton();
			this.radioCryptoRng = new System.Windows.Forms.RadioButton();
			this.radioDoorstop = new System.Windows.Forms.RadioButton();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.labelPlatform = new System.Windows.Forms.Label();
			this.labelExeType = new System.Windows.Forms.Label();
			this.labelIsUnityGame = new System.Windows.Forms.Label();
			this.txtTargetGamePath = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.btnBrowseTarget = new System.Windows.Forms.Button();
			this.label1 = new System.Windows.Forms.Label();
			this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
			this.tabControl1.SuspendLayout();
			this.tabPage1.SuspendLayout();
			this.tabPage2.SuspendLayout();
			this.groupBox3.SuspendLayout();
			this.groupBox2.SuspendLayout();
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
			// btnPatch
			// 
			this.btnPatch.Location = new System.Drawing.Point(6, 6);
			this.btnPatch.Name = "btnPatch";
			this.btnPatch.Size = new System.Drawing.Size(585, 23);
			this.btnPatch.TabIndex = 0;
			this.btnPatch.Text = "Patch";
			this.btnPatch.UseVisualStyleBackColor = true;
			// 
			// tabPage2
			// 
			this.tabPage2.Controls.Add(this.button2);
			this.tabPage2.Controls.Add(this.groupBox3);
			this.tabPage2.Controls.Add(this.checkRuntimePatches);
			this.tabPage2.Controls.Add(this.btnAdvancedPatch);
			this.tabPage2.Controls.Add(this.groupBox2);
			this.tabPage2.Location = new System.Drawing.Point(4, 22);
			this.tabPage2.Name = "tabPage2";
			this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
			this.tabPage2.Size = new System.Drawing.Size(597, 271);
			this.tabPage2.TabIndex = 1;
			this.tabPage2.Text = "Advanced";
			this.tabPage2.UseVisualStyleBackColor = true;
			// 
			// button2
			// 
			this.button2.Location = new System.Drawing.Point(422, 242);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(167, 23);
			this.button2.TabIndex = 5;
			this.button2.Text = "Export config";
			this.button2.UseVisualStyleBackColor = true;
			// 
			// groupBox3
			// 
			this.groupBox3.Controls.Add(this.cmbTargetMethod);
			this.groupBox3.Controls.Add(this.label5);
			this.groupBox3.Controls.Add(this.cmbTargetClass);
			this.groupBox3.Controls.Add(this.label4);
			this.groupBox3.Controls.Add(this.cmbTargetAssembly);
			this.groupBox3.Controls.Add(this.label3);
			this.groupBox3.Location = new System.Drawing.Point(156, 6);
			this.groupBox3.Name = "groupBox3";
			this.groupBox3.Size = new System.Drawing.Size(433, 128);
			this.groupBox3.TabIndex = 4;
			this.groupBox3.TabStop = false;
			this.groupBox3.Text = "Chainloader";
			// 
			// cmbTargetMethod
			// 
			this.cmbTargetMethod.FormattingEnabled = true;
			this.cmbTargetMethod.Location = new System.Drawing.Point(99, 72);
			this.cmbTargetMethod.Name = "cmbTargetMethod";
			this.cmbTargetMethod.Size = new System.Drawing.Size(328, 21);
			this.cmbTargetMethod.TabIndex = 8;
			this.cmbTargetMethod.Text = ".cctor";
			// 
			// label5
			// 
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(6, 75);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(79, 13);
			this.label5.TabIndex = 7;
			this.label5.Text = "Target method:";
			// 
			// cmbTargetClass
			// 
			this.cmbTargetClass.FormattingEnabled = true;
			this.cmbTargetClass.Location = new System.Drawing.Point(99, 45);
			this.cmbTargetClass.Name = "cmbTargetClass";
			this.cmbTargetClass.Size = new System.Drawing.Size(328, 21);
			this.cmbTargetClass.TabIndex = 6;
			this.cmbTargetClass.Text = "Application";
			// 
			// label4
			// 
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(6, 48);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(68, 13);
			this.label4.TabIndex = 5;
			this.label4.Text = "Target class:";
			// 
			// cmbTargetAssembly
			// 
			this.cmbTargetAssembly.FormattingEnabled = true;
			this.cmbTargetAssembly.Location = new System.Drawing.Point(99, 18);
			this.cmbTargetAssembly.Name = "cmbTargetAssembly";
			this.cmbTargetAssembly.Size = new System.Drawing.Size(328, 21);
			this.cmbTargetAssembly.TabIndex = 4;
			this.cmbTargetAssembly.Text = "UnityEngine.dll";
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(6, 21);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(87, 13);
			this.label3.TabIndex = 3;
			this.label3.Text = "Target assembly:";
			// 
			// checkRuntimePatches
			// 
			this.checkRuntimePatches.AutoSize = true;
			this.checkRuntimePatches.Checked = true;
			this.checkRuntimePatches.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkRuntimePatches.Location = new System.Drawing.Point(14, 105);
			this.checkRuntimePatches.Name = "checkRuntimePatches";
			this.checkRuntimePatches.Size = new System.Drawing.Size(130, 17);
			this.checkRuntimePatches.TabIndex = 2;
			this.checkRuntimePatches.Text = "Apply runtime patches";
			this.checkRuntimePatches.UseVisualStyleBackColor = true;
			// 
			// btnAdvancedPatch
			// 
			this.btnAdvancedPatch.Location = new System.Drawing.Point(6, 242);
			this.btnAdvancedPatch.Name = "btnAdvancedPatch";
			this.btnAdvancedPatch.Size = new System.Drawing.Size(410, 23);
			this.btnAdvancedPatch.TabIndex = 1;
			this.btnAdvancedPatch.Text = "Patch";
			this.btnAdvancedPatch.UseVisualStyleBackColor = true;
			this.btnAdvancedPatch.Click += new System.EventHandler(this.btnAdvancedPatch_Click);
			// 
			// groupBox2
			// 
			this.groupBox2.Controls.Add(this.radioPatch);
			this.groupBox2.Controls.Add(this.radioCryptoRng);
			this.groupBox2.Controls.Add(this.radioDoorstop);
			this.groupBox2.Location = new System.Drawing.Point(8, 6);
			this.groupBox2.Name = "groupBox2";
			this.groupBox2.Size = new System.Drawing.Size(142, 93);
			this.groupBox2.TabIndex = 0;
			this.groupBox2.TabStop = false;
			this.groupBox2.Text = "Entrypoint";
			// 
			// radioPatch
			// 
			this.radioPatch.AutoSize = true;
			this.radioPatch.Location = new System.Drawing.Point(11, 65);
			this.radioPatch.Name = "radioPatch";
			this.radioPatch.Size = new System.Drawing.Size(113, 17);
			this.radioPatch.TabIndex = 2;
			this.radioPatch.Text = "UnityEngine.dll (IL)";
			this.radioPatch.UseVisualStyleBackColor = true;
			// 
			// radioCryptoRng
			// 
			this.radioCryptoRng.AutoSize = true;
			this.radioCryptoRng.Location = new System.Drawing.Point(11, 42);
			this.radioCryptoRng.Name = "radioCryptoRng";
			this.radioCryptoRng.Size = new System.Drawing.Size(79, 17);
			this.radioCryptoRng.TabIndex = 1;
			this.radioCryptoRng.Text = "CryptoRNG";
			this.toolTip1.SetToolTip(this.radioCryptoRng, resources.GetString("radioCryptoRng.ToolTip"));
			this.radioCryptoRng.UseVisualStyleBackColor = true;
			// 
			// radioDoorstop
			// 
			this.radioDoorstop.AutoSize = true;
			this.radioDoorstop.Checked = true;
			this.radioDoorstop.Location = new System.Drawing.Point(11, 19);
			this.radioDoorstop.Name = "radioDoorstop";
			this.radioDoorstop.Size = new System.Drawing.Size(118, 17);
			this.radioDoorstop.TabIndex = 0;
			this.radioDoorstop.TabStop = true;
			this.radioDoorstop.Text = "UnityDoorstop (IAT)";
			this.toolTip1.SetToolTip(this.radioDoorstop, "(Windows only)\r\n\r\nThe standard BepInEx entrypoint. Places a .dll file next to the" +
        " game executable without overwriting anything.\r\nAllows preloader patches.");
			this.radioDoorstop.UseVisualStyleBackColor = true;
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
			// labelPlatform
			// 
			this.labelPlatform.AutoSize = true;
			this.labelPlatform.Location = new System.Drawing.Point(111, 76);
			this.labelPlatform.Name = "labelPlatform";
			this.labelPlatform.Size = new System.Drawing.Size(53, 13);
			this.labelPlatform.TabIndex = 5;
			this.labelPlatform.Text = "Unknown";
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
			// labelIsUnityGame
			// 
			this.labelIsUnityGame.AutoSize = true;
			this.labelIsUnityGame.Location = new System.Drawing.Point(111, 50);
			this.labelIsUnityGame.Name = "labelIsUnityGame";
			this.labelIsUnityGame.Size = new System.Drawing.Size(53, 13);
			this.labelIsUnityGame.TabIndex = 3;
			this.labelIsUnityGame.Text = "Unknown";
			// 
			// txtTargetGamePath
			// 
			this.txtTargetGamePath.Location = new System.Drawing.Point(87, 21);
			this.txtTargetGamePath.Name = "txtTargetGamePath";
			this.txtTargetGamePath.ReadOnly = true;
			this.txtTargetGamePath.Size = new System.Drawing.Size(488, 20);
			this.txtTargetGamePath.TabIndex = 2;
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
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(12, 9);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(74, 13);
			this.label1.TabIndex = 2;
			this.label1.Text = "BepInEx 5.0.0";
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
			this.tabPage2.ResumeLayout(false);
			this.tabPage2.PerformLayout();
			this.groupBox3.ResumeLayout(false);
			this.groupBox3.PerformLayout();
			this.groupBox2.ResumeLayout(false);
			this.groupBox2.PerformLayout();
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
		private System.Windows.Forms.GroupBox groupBox3;
		private System.Windows.Forms.ComboBox cmbTargetMethod;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.ComboBox cmbTargetClass;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.ComboBox cmbTargetAssembly;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.CheckBox checkRuntimePatches;
		private System.Windows.Forms.Button btnAdvancedPatch;
		private System.Windows.Forms.GroupBox groupBox2;
		private System.Windows.Forms.RadioButton radioPatch;
		private System.Windows.Forms.RadioButton radioCryptoRng;
		private System.Windows.Forms.RadioButton radioDoorstop;
		private System.Windows.Forms.ToolTip toolTip1;
		private System.Windows.Forms.Button button2;
	}
}

