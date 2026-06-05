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
      this.tvFolders = new System.Windows.Forms.TreeView();
      this.btnAddRoot = new System.Windows.Forms.Button();
      this.btnAddOverride = new System.Windows.Forms.Button();
      this.btnRemove = new System.Windows.Forms.Button();
      this.gbFolder = new System.Windows.Forms.GroupBox();
      this.cbOvParity = new System.Windows.Forms.CheckBox();
      this.lblParity = new System.Windows.Forms.Label();
      this.nudParityPercent = new System.Windows.Forms.NumericUpDown();
      this.cbOvAutoRepair = new System.Windows.Forms.CheckBox();
      this.cbAutoRepair = new System.Windows.Forms.CheckBox();
      this.cbOvVerifySchedule = new System.Windows.Forms.CheckBox();
      this.lblVerifySchedule = new System.Windows.Forms.Label();
      this.tbVerifySchedule = new System.Windows.Forms.TextBox();
      this.cbOvBackupPath = new System.Windows.Forms.CheckBox();
      this.lblBackupPath = new System.Windows.Forms.Label();
      this.tbBackupPath = new System.Windows.Forms.TextBox();
      this.btnBrowseBackup = new System.Windows.Forms.Button();
      this.cbOvBackupSchedule = new System.Windows.Forms.CheckBox();
      this.lblBackupSchedule = new System.Windows.Forms.Label();
      this.tbBackupSchedule = new System.Windows.Forms.TextBox();
      this.cbOvGfs = new System.Windows.Forms.CheckBox();
      this.lblGfs = new System.Windows.Forms.Label();
      this.nudGfsDaily = new System.Windows.Forms.NumericUpDown();
      this.nudGfsWeekly = new System.Windows.Forms.NumericUpDown();
      this.nudGfsMonthly = new System.Windows.Forms.NumericUpDown();
      this.cbOvRefresh = new System.Windows.Forms.CheckBox();
      this.lblRefresh = new System.Windows.Forms.Label();
      this.nudRefreshDays = new System.Windows.Forms.NumericUpDown();
      this.cbOvCommand = new System.Windows.Forms.CheckBox();
      this.lblCommand = new System.Windows.Forms.Label();
      this.tbCommand = new System.Windows.Forms.TextBox();
      this.cbOvDedup = new System.Windows.Forms.CheckBox();
      this.cbDedup = new System.Windows.Forms.CheckBox();
      this.cbOvDegradation = new System.Windows.Forms.CheckBox();
      this.lblDegradation = new System.Windows.Forms.Label();
      this.nudDegradation = new System.Windows.Forms.NumericUpDown();
      this.cbOvToasts = new System.Windows.Forms.CheckBox();
      this.cbToasts = new System.Windows.Forms.CheckBox();
      this.lblGlobalSchedule = new System.Windows.Forms.Label();
      this.tbGlobalSchedule = new System.Windows.Forms.TextBox();
      this.btnOk = new System.Windows.Forms.Button();
      this.btnCancel = new System.Windows.Forms.Button();
      this.gbFolder.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this.nudParityPercent)).BeginInit();
      ((System.ComponentModel.ISupportInitialize)(this.nudGfsDaily)).BeginInit();
      ((System.ComponentModel.ISupportInitialize)(this.nudGfsWeekly)).BeginInit();
      ((System.ComponentModel.ISupportInitialize)(this.nudGfsMonthly)).BeginInit();
      ((System.ComponentModel.ISupportInitialize)(this.nudRefreshDays)).BeginInit();
      ((System.ComponentModel.ISupportInitialize)(this.nudDegradation)).BeginInit();
      this.SuspendLayout();
      //
      // tvFolders
      //
      this.tvFolders.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left)));
      this.tvFolders.HideSelection = false;
      this.tvFolders.Location = new System.Drawing.Point(12, 12);
      this.tvFolders.Name = "tvFolders";
      this.tvFolders.ShowNodeToolTips = true;
      this.tvFolders.Size = new System.Drawing.Size(250, 330);
      this.tvFolders.TabIndex = 0;
      this.tvFolders.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.tvFolders_AfterSelect);
      //
      // btnAddRoot
      //
      this.btnAddRoot.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.btnAddRoot.Location = new System.Drawing.Point(12, 348);
      this.btnAddRoot.Name = "btnAddRoot";
      this.btnAddRoot.Size = new System.Drawing.Size(80, 25);
      this.btnAddRoot.TabIndex = 1;
      this.btnAddRoot.Text = "Add root...";
      this.btnAddRoot.UseVisualStyleBackColor = true;
      this.btnAddRoot.Click += new System.EventHandler(this.btnAddRoot_Click);
      //
      // btnAddOverride
      //
      this.btnAddOverride.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.btnAddOverride.Location = new System.Drawing.Point(96, 348);
      this.btnAddOverride.Name = "btnAddOverride";
      this.btnAddOverride.Size = new System.Drawing.Size(96, 25);
      this.btnAddOverride.TabIndex = 2;
      this.btnAddOverride.Text = "Add override...";
      this.btnAddOverride.UseVisualStyleBackColor = true;
      this.btnAddOverride.Click += new System.EventHandler(this.btnAddOverride_Click);
      //
      // btnRemove
      //
      this.btnRemove.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.btnRemove.Location = new System.Drawing.Point(196, 348);
      this.btnRemove.Name = "btnRemove";
      this.btnRemove.Size = new System.Drawing.Size(66, 25);
      this.btnRemove.TabIndex = 3;
      this.btnRemove.Text = "Remove";
      this.btnRemove.UseVisualStyleBackColor = true;
      this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
      //
      // gbFolder
      //
      this.gbFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
      this.gbFolder.Controls.Add(this.cbOvParity);
      this.gbFolder.Controls.Add(this.lblParity);
      this.gbFolder.Controls.Add(this.nudParityPercent);
      this.gbFolder.Controls.Add(this.cbOvAutoRepair);
      this.gbFolder.Controls.Add(this.cbAutoRepair);
      this.gbFolder.Controls.Add(this.cbOvVerifySchedule);
      this.gbFolder.Controls.Add(this.lblVerifySchedule);
      this.gbFolder.Controls.Add(this.tbVerifySchedule);
      this.gbFolder.Controls.Add(this.cbOvBackupPath);
      this.gbFolder.Controls.Add(this.lblBackupPath);
      this.gbFolder.Controls.Add(this.tbBackupPath);
      this.gbFolder.Controls.Add(this.btnBrowseBackup);
      this.gbFolder.Controls.Add(this.cbOvBackupSchedule);
      this.gbFolder.Controls.Add(this.lblBackupSchedule);
      this.gbFolder.Controls.Add(this.tbBackupSchedule);
      this.gbFolder.Controls.Add(this.cbOvGfs);
      this.gbFolder.Controls.Add(this.lblGfs);
      this.gbFolder.Controls.Add(this.nudGfsDaily);
      this.gbFolder.Controls.Add(this.nudGfsWeekly);
      this.gbFolder.Controls.Add(this.nudGfsMonthly);
      this.gbFolder.Controls.Add(this.cbOvRefresh);
      this.gbFolder.Controls.Add(this.lblRefresh);
      this.gbFolder.Controls.Add(this.nudRefreshDays);
      this.gbFolder.Controls.Add(this.cbOvCommand);
      this.gbFolder.Controls.Add(this.lblCommand);
      this.gbFolder.Controls.Add(this.tbCommand);
      this.gbFolder.Controls.Add(this.cbOvDedup);
      this.gbFolder.Controls.Add(this.cbDedup);
      this.gbFolder.Controls.Add(this.cbOvDegradation);
      this.gbFolder.Controls.Add(this.lblDegradation);
      this.gbFolder.Controls.Add(this.nudDegradation);
      this.gbFolder.Controls.Add(this.cbOvToasts);
      this.gbFolder.Controls.Add(this.cbToasts);
      this.gbFolder.Location = new System.Drawing.Point(270, 12);
      this.gbFolder.Name = "gbFolder";
      this.gbFolder.Size = new System.Drawing.Size(420, 330);
      this.gbFolder.TabIndex = 4;
      this.gbFolder.TabStop = false;
      this.gbFolder.Text = "Folder policy (check a box to override the inherited value)";
      //
      // row 1: parity
      //
      this.cbOvParity.Location = new System.Drawing.Point(12, 24);
      this.cbOvParity.Name = "cbOvParity";
      this.cbOvParity.Size = new System.Drawing.Size(16, 17);
      this.lblParity.AutoSize = true;
      this.lblParity.Location = new System.Drawing.Point(32, 26);
      this.lblParity.Text = "Parity redundancy (%):";
      this.nudParityPercent.Location = new System.Drawing.Point(330, 23);
      this.nudParityPercent.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
      this.nudParityPercent.Name = "nudParityPercent";
      this.nudParityPercent.Size = new System.Drawing.Size(75, 20);
      //
      // row 2: auto repair
      //
      this.cbOvAutoRepair.Location = new System.Drawing.Point(12, 50);
      this.cbOvAutoRepair.Name = "cbOvAutoRepair";
      this.cbOvAutoRepair.Size = new System.Drawing.Size(16, 17);
      this.cbAutoRepair.AutoSize = true;
      this.cbAutoRepair.Location = new System.Drawing.Point(32, 50);
      this.cbAutoRepair.Name = "cbAutoRepair";
      this.cbAutoRepair.Text = "Repair detected bit rot automatically";
      //
      // row 3: verify schedule
      //
      this.cbOvVerifySchedule.Location = new System.Drawing.Point(12, 76);
      this.cbOvVerifySchedule.Name = "cbOvVerifySchedule";
      this.cbOvVerifySchedule.Size = new System.Drawing.Size(16, 17);
      this.lblVerifySchedule.AutoSize = true;
      this.lblVerifySchedule.Location = new System.Drawing.Point(32, 78);
      this.lblVerifySchedule.Text = "Verify schedule:";
      this.tbVerifySchedule.Location = new System.Drawing.Point(240, 75);
      this.tbVerifySchedule.Name = "tbVerifySchedule";
      this.tbVerifySchedule.Size = new System.Drawing.Size(165, 20);
      //
      // row 4: backup path
      //
      this.cbOvBackupPath.Location = new System.Drawing.Point(12, 102);
      this.cbOvBackupPath.Name = "cbOvBackupPath";
      this.cbOvBackupPath.Size = new System.Drawing.Size(16, 17);
      this.lblBackupPath.AutoSize = true;
      this.lblBackupPath.Location = new System.Drawing.Point(32, 104);
      this.lblBackupPath.Text = "Backup target:";
      this.tbBackupPath.Location = new System.Drawing.Point(150, 101);
      this.tbBackupPath.Name = "tbBackupPath";
      this.tbBackupPath.Size = new System.Drawing.Size(220, 20);
      this.btnBrowseBackup.Location = new System.Drawing.Point(376, 99);
      this.btnBrowseBackup.Name = "btnBrowseBackup";
      this.btnBrowseBackup.Size = new System.Drawing.Size(29, 23);
      this.btnBrowseBackup.Text = "...";
      this.btnBrowseBackup.UseVisualStyleBackColor = true;
      this.btnBrowseBackup.Click += new System.EventHandler(this.btnBrowseBackup_Click);
      //
      // row 5: backup schedule
      //
      this.cbOvBackupSchedule.Location = new System.Drawing.Point(12, 128);
      this.cbOvBackupSchedule.Name = "cbOvBackupSchedule";
      this.cbOvBackupSchedule.Size = new System.Drawing.Size(16, 17);
      this.lblBackupSchedule.AutoSize = true;
      this.lblBackupSchedule.Location = new System.Drawing.Point(32, 130);
      this.lblBackupSchedule.Text = "Backup schedule (empty = manual only):";
      this.tbBackupSchedule.Location = new System.Drawing.Point(240, 127);
      this.tbBackupSchedule.Name = "tbBackupSchedule";
      this.tbBackupSchedule.Size = new System.Drawing.Size(165, 20);
      //
      // row 6: GFS retention
      //
      this.cbOvGfs.Location = new System.Drawing.Point(12, 154);
      this.cbOvGfs.Name = "cbOvGfs";
      this.cbOvGfs.Size = new System.Drawing.Size(16, 17);
      this.lblGfs.AutoSize = true;
      this.lblGfs.Location = new System.Drawing.Point(32, 156);
      this.lblGfs.Text = "Keep snapshots (daily / weekly / monthly):";
      this.nudGfsDaily.Location = new System.Drawing.Point(255, 153);
      this.nudGfsDaily.Maximum = new decimal(new int[] { 365, 0, 0, 0 });
      this.nudGfsDaily.Name = "nudGfsDaily";
      this.nudGfsDaily.Size = new System.Drawing.Size(45, 20);
      this.nudGfsWeekly.Location = new System.Drawing.Point(305, 153);
      this.nudGfsWeekly.Maximum = new decimal(new int[] { 520, 0, 0, 0 });
      this.nudGfsWeekly.Name = "nudGfsWeekly";
      this.nudGfsWeekly.Size = new System.Drawing.Size(45, 20);
      this.nudGfsMonthly.Location = new System.Drawing.Point(355, 153);
      this.nudGfsMonthly.Maximum = new decimal(new int[] { 1200, 0, 0, 0 });
      this.nudGfsMonthly.Name = "nudGfsMonthly";
      this.nudGfsMonthly.Size = new System.Drawing.Size(50, 20);
      //
      // row 7: refresh
      //
      this.cbOvRefresh.Location = new System.Drawing.Point(12, 180);
      this.cbOvRefresh.Name = "cbOvRefresh";
      this.cbOvRefresh.Size = new System.Drawing.Size(16, 17);
      this.lblRefresh.AutoSize = true;
      this.lblRefresh.Location = new System.Drawing.Point(32, 182);
      this.lblRefresh.Text = "Flash refresh interval (days, 0 = off):";
      this.nudRefreshDays.Location = new System.Drawing.Point(330, 179);
      this.nudRefreshDays.Maximum = new decimal(new int[] { 3650, 0, 0, 0 });
      this.nudRefreshDays.Name = "nudRefreshDays";
      this.nudRefreshDays.Size = new System.Drawing.Size(75, 20);
      //
      // row 8: command
      //
      this.cbOvCommand.Location = new System.Drawing.Point(12, 206);
      this.cbOvCommand.Name = "cbOvCommand";
      this.cbOvCommand.Size = new System.Drawing.Size(16, 17);
      this.lblCommand.AutoSize = true;
      this.lblCommand.Location = new System.Drawing.Point(32, 208);
      this.lblCommand.Text = "Command on corruption ({file}, {folder}):";
      this.tbCommand.Location = new System.Drawing.Point(240, 205);
      this.tbCommand.Name = "tbCommand";
      this.tbCommand.Size = new System.Drawing.Size(165, 20);
      //
      // row 9: dedup
      //
      this.cbOvDedup.Location = new System.Drawing.Point(12, 232);
      this.cbOvDedup.Name = "cbOvDedup";
      this.cbOvDedup.Size = new System.Drawing.Size(16, 17);
      this.cbDedup.AutoSize = true;
      this.cbDedup.Location = new System.Drawing.Point(32, 232);
      this.cbDedup.Name = "cbDedup";
      this.cbDedup.Text = "Allow merging duplicates into hard links (NTFS)";
      //
      // row 10: degradation threshold
      //
      this.cbOvDegradation.Location = new System.Drawing.Point(12, 258);
      this.cbOvDegradation.Name = "cbOvDegradation";
      this.cbOvDegradation.Size = new System.Drawing.Size(16, 17);
      this.lblDegradation.AutoSize = true;
      this.lblDegradation.Location = new System.Drawing.Point(32, 260);
      this.lblDegradation.Text = "Degradation warning (errors per month):";
      this.nudDegradation.Location = new System.Drawing.Point(330, 257);
      this.nudDegradation.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
      this.nudDegradation.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
      this.nudDegradation.Name = "nudDegradation";
      this.nudDegradation.Size = new System.Drawing.Size(75, 20);
      //
      // row 11: toasts
      //
      this.cbOvToasts.Location = new System.Drawing.Point(12, 284);
      this.cbOvToasts.Name = "cbOvToasts";
      this.cbOvToasts.Size = new System.Drawing.Size(16, 17);
      this.cbToasts.AutoSize = true;
      this.cbToasts.Location = new System.Drawing.Point(32, 284);
      this.cbToasts.Name = "cbToasts";
      this.cbToasts.Text = "Show balloon notifications";
      //
      // global schedule + buttons
      //
      this.lblGlobalSchedule.AutoSize = true;
      this.lblGlobalSchedule.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.lblGlobalSchedule.Location = new System.Drawing.Point(270, 354);
      this.lblGlobalSchedule.Text = "Global default verify schedule:";
      this.tbGlobalSchedule.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.tbGlobalSchedule.Location = new System.Drawing.Point(430, 351);
      this.tbGlobalSchedule.Name = "tbGlobalSchedule";
      this.tbGlobalSchedule.Size = new System.Drawing.Size(120, 20);
      //
      // btnOk / btnCancel
      //
      this.btnOk.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      this.btnOk.Location = new System.Drawing.Point(534, 384);
      this.btnOk.Name = "btnOk";
      this.btnOk.Size = new System.Drawing.Size(75, 25);
      this.btnOk.TabIndex = 10;
      this.btnOk.Text = "OK";
      this.btnOk.UseVisualStyleBackColor = true;
      this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
      this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
      this.btnCancel.Location = new System.Drawing.Point(615, 384);
      this.btnCancel.Name = "btnCancel";
      this.btnCancel.Size = new System.Drawing.Size(75, 25);
      this.btnCancel.TabIndex = 11;
      this.btnCancel.Text = "Cancel";
      this.btnCancel.UseVisualStyleBackColor = true;
      //
      // SettingsForm
      //
      this.AcceptButton = this.btnOk;
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.CancelButton = this.btnCancel;
      this.ClientSize = new System.Drawing.Size(702, 421);
      this.Controls.Add(this.tvFolders);
      this.Controls.Add(this.btnAddRoot);
      this.Controls.Add(this.btnAddOverride);
      this.Controls.Add(this.btnRemove);
      this.Controls.Add(this.gbFolder);
      this.Controls.Add(this.lblGlobalSchedule);
      this.Controls.Add(this.tbGlobalSchedule);
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
      ((System.ComponentModel.ISupportInitialize)(this.nudGfsDaily)).EndInit();
      ((System.ComponentModel.ISupportInitialize)(this.nudGfsWeekly)).EndInit();
      ((System.ComponentModel.ISupportInitialize)(this.nudGfsMonthly)).EndInit();
      ((System.ComponentModel.ISupportInitialize)(this.nudRefreshDays)).EndInit();
      ((System.ComponentModel.ISupportInitialize)(this.nudDegradation)).EndInit();
      this.ResumeLayout(false);
      this.PerformLayout();
    }

    #endregion

    private System.Windows.Forms.TreeView tvFolders;
    private System.Windows.Forms.Button btnAddRoot;
    private System.Windows.Forms.Button btnAddOverride;
    private System.Windows.Forms.Button btnRemove;
    private System.Windows.Forms.GroupBox gbFolder;
    private System.Windows.Forms.CheckBox cbOvParity;
    private System.Windows.Forms.Label lblParity;
    private System.Windows.Forms.NumericUpDown nudParityPercent;
    private System.Windows.Forms.CheckBox cbOvAutoRepair;
    private System.Windows.Forms.CheckBox cbAutoRepair;
    private System.Windows.Forms.CheckBox cbOvVerifySchedule;
    private System.Windows.Forms.Label lblVerifySchedule;
    private System.Windows.Forms.TextBox tbVerifySchedule;
    private System.Windows.Forms.CheckBox cbOvBackupPath;
    private System.Windows.Forms.Label lblBackupPath;
    private System.Windows.Forms.TextBox tbBackupPath;
    private System.Windows.Forms.Button btnBrowseBackup;
    private System.Windows.Forms.CheckBox cbOvBackupSchedule;
    private System.Windows.Forms.Label lblBackupSchedule;
    private System.Windows.Forms.TextBox tbBackupSchedule;
    private System.Windows.Forms.CheckBox cbOvGfs;
    private System.Windows.Forms.Label lblGfs;
    private System.Windows.Forms.NumericUpDown nudGfsDaily;
    private System.Windows.Forms.NumericUpDown nudGfsWeekly;
    private System.Windows.Forms.NumericUpDown nudGfsMonthly;
    private System.Windows.Forms.CheckBox cbOvRefresh;
    private System.Windows.Forms.Label lblRefresh;
    private System.Windows.Forms.NumericUpDown nudRefreshDays;
    private System.Windows.Forms.CheckBox cbOvCommand;
    private System.Windows.Forms.Label lblCommand;
    private System.Windows.Forms.TextBox tbCommand;
    private System.Windows.Forms.CheckBox cbOvDedup;
    private System.Windows.Forms.CheckBox cbDedup;
    private System.Windows.Forms.CheckBox cbOvDegradation;
    private System.Windows.Forms.Label lblDegradation;
    private System.Windows.Forms.NumericUpDown nudDegradation;
    private System.Windows.Forms.CheckBox cbOvToasts;
    private System.Windows.Forms.CheckBox cbToasts;
    private System.Windows.Forms.Label lblGlobalSchedule;
    private System.Windows.Forms.TextBox tbGlobalSchedule;
    private System.Windows.Forms.Button btnOk;
    private System.Windows.Forms.Button btnCancel;
  }
}
