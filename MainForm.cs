using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Filesystem_Toolbox.Properties;

namespace Filesystem_Toolbox {
  internal partial class MainForm : Form {

    #region nested types

    private class DgvEntry {
      private readonly FileInfo _file;
      private readonly Exception _exception;

      [Browsable(false)]
      public FileInfo File => this._file;

      public Image Image { get; }
      public string FileName => this._file.Name;
      public string Extension => this._file.Extension;
      public string Path => this._file.Directory?.FullName;
      public string Checksum { get; }
      public string OldChecksum { get; }
      public string Exception => this._exception?.Message;

      private DgvEntry(FileInfo file) {
        this._file = file;
      }

      private DgvEntry(FileInfo file, string oldChecksum, string currentChecksum) : this(file) {
        this.Checksum = currentChecksum;
        this.OldChecksum = oldChecksum;
        this.Image = Resources._16x16_Warning;
      }

      private DgvEntry(FileInfo file, string oldChecksum, Exception exception) : this(file) {
        this._exception = exception;
        this.OldChecksum = oldChecksum;
        this.Image = Resources._16x16_Error;
      }

      public static DgvEntry FromFailedChecksum(FileInfo file, string old, string current) => new DgvEntry(
        file,
        old,
        current);

      public static DgvEntry FromException(FileInfo file, string old, Exception exception) => new DgvEntry(
        file,
        old,
        exception);
    }

    #endregion

    private readonly SortableBindingList<DgvEntry> _entries = new SortableBindingList<DgvEntry>();

    private bool _verificationRunning;

    internal bool VerificationRunning {
      get { return this._verificationRunning; }
      set {
        this._verificationRunning = value;
        this.SafelyInvoke(() => {
          this.tsmiVerifyFolders.Enabled = !(this.tsslVerificationRunning.Visible = value);
        });
      }
    }

    private readonly MainLogic _logic;

    internal MainForm(MainLogic logic = null) {
      this._logic = logic;
      this.InitializeComponent();
      this.SetFormTitle();

      this.dgvProblems.DataSource = this._entries;
      this.tCheckTimer.Interval = (int)Settings.Default.CheckInterval.TotalMilliseconds;
      this.tCheckTimer.Start();
    }

    internal void MarkFileChecksumFailed(FileInfo file, string oldChecksum, string newChecksum)
      => this.SafelyInvoke(() => this._AddEntry(DgvEntry.FromFailedChecksum(file, oldChecksum, newChecksum)));

    internal void MarkFileException(FileInfo file, string oldChecksum, Exception exception)
      => this.SafelyInvoke(() => this._AddEntry(DgvEntry.FromException(file, oldChecksum, exception)));

    private void _AddEntry(DgvEntry entry) {
      if (entry == null)
        throw new ArgumentNullException(nameof(entry));

      var entries = this._entries;
      for (var i = entries.Count - 1; i >= 0; --i)
        if (entries[i].File.FullName == entry.File.FullName)
          entries.RemoveAt(i);

      entries.Add(entry);
    }

    private void MainForm_FormClosing(object sender, FormClosingEventArgs e) {
      if (e.CloseReason != CloseReason.UserClosing)
        return;

      this.Hide();
      e.Cancel = true;
    }

    private void MainForm_Shown(object _, EventArgs __) {
      this.Select();
    }

    private void tsmiShowForm_Click(object _, EventArgs __) {
      this.Show();
      this.Select();
    }

    private void tsmiExitApplication_Click(object _, EventArgs __) => Application.Exit();

    private void tCheckTimer_Tick(object _, EventArgs __) {
      this.tCheckTimer.Stop();
      this.Async(
        () => {
          var isRunning = (bool?)null;
          try {
            isRunning = this.VerificationRunning;
            if (isRunning.Value)
              return;

            this.VerificationRunning = true;
            this._logic?.RunChecks(this.MarkFileChecksumFailed, this.MarkFileException);
          } finally {
            if (isRunning != null && !isRunning.Value)
              this.VerificationRunning = false;

            this.tCheckTimer.Start();
          }
        }
      );
    }

    private void tsmiVerifyFolders_Click(object _, EventArgs __) => this.tCheckTimer_Tick(null, null);

    private void tsmiRebuildDatabase_Click(object _, EventArgs __) {
      this.tsmiRebuildDatabase.Enabled = false;
      this.Async(
        () => {
          try {
            this._logic?.RebuildDatabases();
          } finally {
            this.SafelyInvoke(() => {
              this.tsmiRebuildDatabase.Enabled = true;
            });
          }
        }
      );
    }

  }
}