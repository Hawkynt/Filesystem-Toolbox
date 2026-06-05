namespace Filesystem_Toolbox {
  partial class SettingsForm {
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing) {
      if (disposing && (components != null)) {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent() {
      this.lbFolders = new System.Windows.Forms.ListBox();
      this.btnAddFolder = new System.Windows.Forms.Button();
      this.btnRemoveFolder = new System.Windows.Forms.Button();
      this.gbFolder = new System.Windows.Forms.GroupBox();
      this.lblParityPercent = new System.Windows.Forms.Label();
      this.nudParityPercent = new System.Windows.Forms.NumericUpDown();
      this.cbAutoRepair = new System.Windows.Forms.CheckBox();
      this.lblMirrorPath = new System.Windows.Forms.Label();
      this.tbMirrorPath = new System.Windows.Forms.TextBox();
      this.btnBrowseMirror = new System.Windows.Forms.Button();
      this.lblRefreshDays = new System.Windows.Forms.Label();
      this.nudRefreshDays = new System.Windows.Forms.NumericUpDown();
      this.lblCommand = new System.Windows.Forms.Label();
      this.tbCommand = new System.Windows.Forms.TextBox();
      this.cbDedup = new System.Windows.Forms.CheckBox();
      this.lblCheckInterval = new System.Windows.Forms.Label();
      this.nudCheckInterval = new System.Windows.Forms.NumericUpDown();
      this.btnOk = new System.Windows.Forms.Button();
      this.btnCancel = new System.Windows.Forms.Button();
      this.gbFolder.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this.nudParityPercent)).BeginInit();
      ((System.ComponentModel.ISupportInitialize)(this.nudRefreshDays)).BeginInit();
      ((System.ComponentModel.ISupportInitialize)(this.nudCheckInterval)).BeginInit();
      this.SuspendLayout();
      //
      // lbFolders
      //
      this.lbFolders.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left)));
      this.lbFolders.IntegralHeight = false;
      this.lbFolders.Location = new System.Drawing.Point(12, 12);
      this.lbFolders.Name = "lbFolders";
      this.lbFolders.Size = new System.Drawing.Size(240, 250);
      this.lbFolders.TabIndex = 0;
      this.lbFolders.SelectedIndexChanged += new System.EventHandler(this.lbFolders_SelectedIndexChanged);
      //
      // btnAddFolder
      //
      this.btnAddFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.btnAddFolder.Location = new System.Drawing.Point(12, 268);
      this.btnAddFolder.Name = "btnAddFolder";
      this.btnAddFolder.Size = new System.Drawing.Size(117, 25);
      this.btnAddFolder.TabIndex = 1;
      this.btnAddFolder.Text = "Add...";
      this.btnAddFolder.UseVisualStyleBackColor = true;
      this.btnAddFolder.Click += new System.EventHandler(this.btnAddFolder_Click);
      //
      // btnRemoveFolder
      //
      this.btnRemoveFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.btnRemoveFolder.Location = new System.Drawing.Point(135, 268);
      this.btnRemoveFolder.Name = "btnRemoveFolder";
      this.btnRemoveFolder.Size = new System.Drawing.Size(117, 25);
      this.btnRemoveFolder.TabIndex = 2;
      this.btnRemoveFolder.Text = "Remove";
      this.btnRemoveFolder.UseVisualStyleBackColor = true;
      this.btnRemoveFolder.Click += new System.EventHandler(this.btnRemoveFolder_Click);
      //
      // gbFolder
      //
      this.gbFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
      this.gbFolder.Controls.Add(this.lblParityPercent);
      this.gbFolder.Controls.Add(this.nudParityPercent);
      this.gbFolder.Controls.Add(this.cbAutoRepair);
      this.gbFolder.Controls.Add(this.lblMirrorPath);
      this.gbFolder.Controls.Add(this.tbMirrorPath);
      this.gbFolder.Controls.Add(this.btnBrowseMirror);
      this.gbFolder.Controls.Add(this.lblRefreshDays);
      this.gbFolder.Controls.Add(this.nudRefreshDays);
      this.gbFolder.Controls.Add(this.lblCommand);
      this.gbFolder.Controls.Add(this.tbCommand);
      this.gbFolder.Controls.Add(this.cbDedup);
      this.gbFolder.Location = new System.Drawing.Point(258, 12);
      this.gbFolder.Name = "gbFolder";
      this.gbFolder.Size = new System.Drawing.Size(330, 250);
      this.gbFolder.TabIndex = 3;
      this.gbFolder.TabStop = false;
      this.gbFolder.Text = "Folder policy";
      //
      // lblParityPercent
      //
      this.lblParityPercent.AutoSize = true;
      this.lblParityPercent.Location = new System.Drawing.Point(12, 26);
      this.lblParityPercent.Name = "lblParityPercent";
      this.lblParityPercent.Size = new System.Drawing.Size(120, 13);
      this.lblParityPercent.Text = "Parity redundancy (%):";
      //
      // nudParityPercent
      //
      this.nudParityPercent.Location = new System.Drawing.Point(240, 24);
      this.nudParityPercent.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
      this.nudParityPercent.Name = "nudParityPercent";
      this.nudParityPercent.Size = new System.Drawing.Size(75, 20);
      this.nudParityPercent.TabIndex = 0;
      this.nudParityPercent.ValueChanged += new System.EventHandler(this.OnFolderSettingChanged);
      //
      // cbAutoRepair
      //
      this.cbAutoRepair.AutoSize = true;
      this.cbAutoRepair.Location = new System.Drawing.Point(15, 52);
      this.cbAutoRepair.Name = "cbAutoRepair";
      this.cbAutoRepair.Size = new System.Drawing.Size(200, 17);
      this.cbAutoRepair.TabIndex = 1;
      this.cbAutoRepair.Text = "Repair detected bit rot automatically";
      this.cbAutoRepair.UseVisualStyleBackColor = true;
      this.cbAutoRepair.CheckedChanged += new System.EventHandler(this.OnFolderSettingChanged);
      //
      // lblMirrorPath
      //
      this.lblMirrorPath.AutoSize = true;
      this.lblMirrorPath.Location = new System.Drawing.Point(12, 81);
      this.lblMirrorPath.Name = "lblMirrorPath";
      this.lblMirrorPath.Size = new System.Drawing.Size(70, 13);
      this.lblMirrorPath.Text = "Mirror folder:";
      //
      // tbMirrorPath
      //
      this.tbMirrorPath.Location = new System.Drawing.Point(15, 97);
      this.tbMirrorPath.Name = "tbMirrorPath";
      this.tbMirrorPath.Size = new System.Drawing.Size(265, 20);
      this.tbMirrorPath.TabIndex = 2;
      this.tbMirrorPath.TextChanged += new System.EventHandler(this.OnFolderSettingChanged);
      //
      // btnBrowseMirror
      //
      this.btnBrowseMirror.Location = new System.Drawing.Point(286, 95);
      this.btnBrowseMirror.Name = "btnBrowseMirror";
      this.btnBrowseMirror.Size = new System.Drawing.Size(29, 23);
      this.btnBrowseMirror.TabIndex = 3;
      this.btnBrowseMirror.Text = "...";
      this.btnBrowseMirror.UseVisualStyleBackColor = true;
      this.btnBrowseMirror.Click += new System.EventHandler(this.btnBrowseMirror_Click);
      //
      // lblRefreshDays
      //
      this.lblRefreshDays.AutoSize = true;
      this.lblRefreshDays.Location = new System.Drawing.Point(12, 128);
      this.lblRefreshDays.Name = "lblRefreshDays";
      this.lblRefreshDays.Size = new System.Drawing.Size(180, 13);
      this.lblRefreshDays.Text = "Flash refresh interval (days, 0 = off):";
      //
      // nudRefreshDays
      //
      this.nudRefreshDays.Location = new System.Drawing.Point(240, 126);
      this.nudRefreshDays.Maximum = new decimal(new int[] { 3650, 0, 0, 0 });
      this.nudRefreshDays.Name = "nudRefreshDays";
      this.nudRefreshDays.Size = new System.Drawing.Size(75, 20);
      this.nudRefreshDays.TabIndex = 4;
      this.nudRefreshDays.ValueChanged += new System.EventHandler(this.OnFolderSettingChanged);
      //
      // lblCommand
      //
      this.lblCommand.AutoSize = true;
      this.lblCommand.Location = new System.Drawing.Point(12, 158);
      this.lblCommand.Name = "lblCommand";
      this.lblCommand.Size = new System.Drawing.Size(250, 13);
      this.lblCommand.Text = "Command on corruption ({file}, {folder} placeholders):";
      //
      // tbCommand
      //
      this.tbCommand.Location = new System.Drawing.Point(15, 174);
      this.tbCommand.Name = "tbCommand";
      this.tbCommand.Size = new System.Drawing.Size(300, 20);
      this.tbCommand.TabIndex = 5;
      this.tbCommand.TextChanged += new System.EventHandler(this.OnFolderSettingChanged);
      //
      // cbDedup
      //
      this.cbDedup.AutoSize = true;
      this.cbDedup.Location = new System.Drawing.Point(15, 205);
      this.cbDedup.Name = "cbDedup";
      this.cbDedup.Size = new System.Drawing.Size(260, 17);
      this.cbDedup.TabIndex = 6;
      this.cbDedup.Text = "Allow merging duplicates into hard links (NTFS)";
      this.cbDedup.UseVisualStyleBackColor = true;
      this.cbDedup.CheckedChanged += new System.EventHandler(this.OnFolderSettingChanged);
      //
      // lblCheckInterval
      //
      this.lblCheckInterval.AutoSize = true;
      this.lblCheckInterval.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.lblCheckInterval.Location = new System.Drawing.Point(258, 274);
      this.lblCheckInterval.Name = "lblCheckInterval";
      this.lblCheckInterval.Size = new System.Drawing.Size(130, 13);
      this.lblCheckInterval.Text = "Check interval (minutes):";
      //
      // nudCheckInterval
      //
      this.nudCheckInterval.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.nudCheckInterval.Location = new System.Drawing.Point(394, 272);
      this.nudCheckInterval.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
      this.nudCheckInterval.Maximum = new decimal(new int[] { 10080, 0, 0, 0 });
      this.nudCheckInterval.Name = "nudCheckInterval";
      this.nudCheckInterval.Size = new System.Drawing.Size(75, 20);
      this.nudCheckInterval.TabIndex = 4;
      this.nudCheckInterval.Value = new decimal(new int[] { 10, 0, 0, 0 });
      //
      // btnOk
      //
      this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      this.btnOk.Location = new System.Drawing.Point(432, 305);
      this.btnOk.Name = "btnOk";
      this.btnOk.Size = new System.Drawing.Size(75, 25);
      this.btnOk.TabIndex = 5;
      this.btnOk.Text = "OK";
      this.btnOk.UseVisualStyleBackColor = true;
      this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
      //
      // btnCancel
      //
      this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this.btnCancel.Location = new System.Drawing.Point(513, 305);
      this.btnCancel.Name = "btnCancel";
      this.btnCancel.Size = new System.Drawing.Size(75, 25);
      this.btnCancel.TabIndex = 6;
      this.btnCancel.Text = "Cancel";
      this.btnCancel.UseVisualStyleBackColor = true;
      //
      // SettingsForm
      //
      this.AcceptButton = this.btnOk;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this.btnCancel;
      this.ClientSize = new System.Drawing.Size(600, 342);
      this.Controls.Add(this.lbFolders);
      this.Controls.Add(this.btnAddFolder);
      this.Controls.Add(this.btnRemoveFolder);
      this.Controls.Add(this.gbFolder);
      this.Controls.Add(this.lblCheckInterval);
      this.Controls.Add(this.nudCheckInterval);
      this.Controls.Add(this.btnOk);
      this.Controls.Add(this.btnCancel);
      this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "SettingsForm";
      this.ShowInTaskbar = false;
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Settings";
      this.gbFolder.ResumeLayout(false);
      this.gbFolder.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)(this.nudParityPercent)).EndInit();
      ((System.ComponentModel.ISupportInitialize)(this.nudRefreshDays)).EndInit();
      ((System.ComponentModel.ISupportInitialize)(this.nudCheckInterval)).EndInit();
      this.ResumeLayout(false);
      this.PerformLayout();
    }

    #endregion

    private System.Windows.Forms.ListBox lbFolders;
    private System.Windows.Forms.Button btnAddFolder;
    private System.Windows.Forms.Button btnRemoveFolder;
    private System.Windows.Forms.GroupBox gbFolder;
    private System.Windows.Forms.Label lblParityPercent;
    private System.Windows.Forms.NumericUpDown nudParityPercent;
    private System.Windows.Forms.CheckBox cbAutoRepair;
    private System.Windows.Forms.Label lblMirrorPath;
    private System.Windows.Forms.TextBox tbMirrorPath;
    private System.Windows.Forms.Button btnBrowseMirror;
    private System.Windows.Forms.Label lblRefreshDays;
    private System.Windows.Forms.NumericUpDown nudRefreshDays;
    private System.Windows.Forms.Label lblCommand;
    private System.Windows.Forms.TextBox tbCommand;
    private System.Windows.Forms.CheckBox cbDedup;
    private System.Windows.Forms.Label lblCheckInterval;
    private System.Windows.Forms.NumericUpDown nudCheckInterval;
    private System.Windows.Forms.Button btnOk;
    private System.Windows.Forms.Button btnCancel;
  }
}
