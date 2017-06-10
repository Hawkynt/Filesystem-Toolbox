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
      System.Windows.Forms.StatusStrip ssStatusBar;
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
      this.dgvProblems = new System.Windows.Forms.DataGridView();
      this.tsslVerificationRunning = new System.Windows.Forms.ToolStripStatusLabel();
      ssStatusBar = new System.Windows.Forms.StatusStrip();
      ((System.ComponentModel.ISupportInitialize)(this.dgvProblems)).BeginInit();
      ssStatusBar.SuspendLayout();
      this.SuspendLayout();
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
      ((System.ComponentModel.ISupportInitialize)(this.dgvProblems)).EndInit();
      ssStatusBar.ResumeLayout(false);
      ssStatusBar.PerformLayout();
      this.ResumeLayout(false);
      this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.DataGridView dgvProblems;
    private System.Windows.Forms.ToolStripStatusLabel tsslVerificationRunning;
  }
}

