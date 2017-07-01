namespace Filesystem_Toolbox {
  partial class MainForm {
    /// <summary>
    /// Erforderliche Designervariable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Verwendete Ressourcen bereinigen.
    /// </summary>
    /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
    protected override void Dispose(bool disposing) {
      if (disposing && (components != null)) {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Vom Windows Form-Designer generierter Code

    /// <summary>
    /// Erforderliche Methode für die Designerunterstützung.
    /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
    /// </summary>
    private void InitializeComponent() {
      this.components = new System.ComponentModel.Container();
      System.Windows.Forms.StatusStrip ssStatusBar;
      System.Windows.Forms.ToolStripMenuItem tsmiExitApplication;
      System.Windows.Forms.ToolStripMenuItem tsmiShowForm;
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
      this.tsslVerificationRunning = new System.Windows.Forms.ToolStripStatusLabel();
      this.dgvProblems = new System.Windows.Forms.DataGridView();
      this.cmsTrayMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
      this.tsmiRebuildDatabase = new System.Windows.Forms.ToolStripMenuItem();
      this.tsmiVerifyFolders = new System.Windows.Forms.ToolStripMenuItem();
      this.tCheckTimer = new System.Windows.Forms.Timer(this.components);
      ssStatusBar = new System.Windows.Forms.StatusStrip();
      tsmiExitApplication = new System.Windows.Forms.ToolStripMenuItem();
      tsmiShowForm = new System.Windows.Forms.ToolStripMenuItem();
      ssStatusBar.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this.dgvProblems)).BeginInit();
      this.cmsTrayMenu.SuspendLayout();
      this.SuspendLayout();
      // 
      // ssStatusBar
      // 
      ssStatusBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsslVerificationRunning});
      ssStatusBar.Location = new System.Drawing.Point(0, 337);
      ssStatusBar.Name = "ssStatusBar";
      ssStatusBar.Size = new System.Drawing.Size(683, 22);
      ssStatusBar.TabIndex = 1;
      ssStatusBar.Text = "statusStrip1";
      // 
      // tsslVerificationRunning
      // 
      this.tsslVerificationRunning.Image = global::Filesystem_Toolbox.Properties.Resources._24x24_Information;
      this.tsslVerificationRunning.Name = "tsslVerificationRunning";
      this.tsslVerificationRunning.Size = new System.Drawing.Size(115, 17);
      this.tsslVerificationRunning.Text = "Verfying folders...";
      this.tsslVerificationRunning.Visible = false;
      // 
      // tsmiExitApplication
      // 
      tsmiExitApplication.Image = global::Filesystem_Toolbox.Properties.Resources._24x24_Exit_Blue;
      tsmiExitApplication.Name = "tsmiExitApplication";
      tsmiExitApplication.Size = new System.Drawing.Size(114, 22);
      tsmiExitApplication.Text = "Exit";
      tsmiExitApplication.Click += new System.EventHandler(this.tsmiExitApplication_Click);
      // 
      // tsmiShowForm
      // 
      tsmiShowForm.Image = ((System.Drawing.Image)(resources.GetObject("tsmiShowForm.Image")));
      tsmiShowForm.Name = "tsmiShowForm";
      tsmiShowForm.Size = new System.Drawing.Size(114, 22);
      tsmiShowForm.Text = "Show";
      tsmiShowForm.Click += new System.EventHandler(this.tsmiShowForm_Click);
      // 
      // dgvProblems
      // 
      this.dgvProblems.AllowUserToAddRows = false;
      this.dgvProblems.AllowUserToDeleteRows = false;
      this.dgvProblems.AllowUserToResizeRows = false;
      this.dgvProblems.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
      this.dgvProblems.BackgroundColor = System.Drawing.SystemColors.Window;
      this.dgvProblems.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      this.dgvProblems.Dock = System.Windows.Forms.DockStyle.Fill;
      this.dgvProblems.Location = new System.Drawing.Point(0, 0);
      this.dgvProblems.MultiSelect = false;
      this.dgvProblems.Name = "dgvProblems";
      this.dgvProblems.ReadOnly = true;
      this.dgvProblems.RowHeadersVisible = false;
      this.dgvProblems.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
      this.dgvProblems.Size = new System.Drawing.Size(683, 337);
      this.dgvProblems.TabIndex = 0;
      // 
      // cmsTrayMenu
      // 
      this.cmsTrayMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            tsmiShowForm,
            this.tsmiRebuildDatabase,
            this.tsmiVerifyFolders,
            tsmiExitApplication});
      this.cmsTrayMenu.Name = "cmsTrayMenu";
      this.cmsTrayMenu.Size = new System.Drawing.Size(115, 92);
      // 
      // tsmiRebuildDatabase
      // 
      this.tsmiRebuildDatabase.Name = "tsmiRebuildDatabase";
      this.tsmiRebuildDatabase.Size = new System.Drawing.Size(114, 22);
      this.tsmiRebuildDatabase.Text = "Rebuild";
      this.tsmiRebuildDatabase.Click += new System.EventHandler(this.tsmiRebuildDatabase_Click);
      // 
      // tsmiVerifyFolders
      // 
      this.tsmiVerifyFolders.Image = global::Filesystem_Toolbox.Properties.Resources._24x24_Verify_Folders;
      this.tsmiVerifyFolders.Name = "tsmiVerifyFolders";
      this.tsmiVerifyFolders.Size = new System.Drawing.Size(114, 22);
      this.tsmiVerifyFolders.Text = "Verify";
      this.tsmiVerifyFolders.Click += new System.EventHandler(this.tsmiVerifyFolders_Click);
      // 
      // tCheckTimer
      // 
      this.tCheckTimer.Tick += new System.EventHandler(this.tCheckTimer_Tick);
      // 
      // MainForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(683, 359);
      this.Controls.Add(this.dgvProblems);
      this.Controls.Add(ssStatusBar);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.Name = "MainForm";
      this.Text = "Form1";
      this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
      this.Shown += new System.EventHandler(this.MainForm_Shown);
      ssStatusBar.ResumeLayout(false);
      ssStatusBar.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)(this.dgvProblems)).EndInit();
      this.cmsTrayMenu.ResumeLayout(false);
      this.ResumeLayout(false);
      this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.DataGridView dgvProblems;
    private System.Windows.Forms.ToolStripStatusLabel tsslVerificationRunning;
    internal System.Windows.Forms.ContextMenuStrip cmsTrayMenu;
    internal System.Windows.Forms.ToolStripMenuItem tsmiVerifyFolders;
    internal System.Windows.Forms.ToolStripMenuItem tsmiRebuildDatabase;
    private System.Windows.Forms.Timer tCheckTimer;
  }
}

