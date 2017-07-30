using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Classes;
using Filesystem_Toolbox.Properties;

namespace Filesystem_Toolbox {
  internal partial class MainForm : Form {

    #region nested types

    private struct WindowStatus {
      public WindowStatus(string text) {
        this.Text = text;
        this.StartTimeUtc = DateTime.UtcNow;
      }

      public bool IsActionRunning => !string.IsNullOrWhiteSpace(this.Text);
      public string Text { get; }
      public DateTime StartTimeUtc { get; }
      public TimeSpan RunTime => DateTime.UtcNow - this.StartTimeUtc;
      public static WindowStatus Empty { get; } = new WindowStatus(null);
    }

    private class DgvEntry {
      private readonly DirectoryInfo _root;
      private readonly FileInfo _file;
      private readonly Exception _exception;

      [Browsable(false)]
      public FileInfo File => this._file;

      [DataGridViewColumnWidth(24)]
      public Image Image { get; }
      public string FileName => this._file.Name;
      public string Extension => this._file.Extension;
      public string Name => this._file.GetFilenameWithoutExtension();
      public string FolderName => this._file.Directory?.Name;
      public string RelativePath => this._file.Directory?.RelativeTo(this._root);
      public string Path => this._file.Directory?.FullName;
      public string Checksum { get; }
      public string OldChecksum { get; }
      public string Exception => this._exception?.Message;

      private DgvEntry(DirectoryInfo root, FileInfo file) {
        this._root = root;
        this._file = file;
      }

      private DgvEntry(DirectoryInfo root, FileInfo file, string oldChecksum, string currentChecksum) : this(root, file) {
        this.Checksum = currentChecksum;
        this.OldChecksum = oldChecksum;
        this.Image = Resources._16x16_Warning;
      }

      private DgvEntry(DirectoryInfo root, FileInfo file, string oldChecksum, Exception exception) : this(root, file) {
        this._exception = exception;
        this.OldChecksum = oldChecksum;
        this.Image = Resources._16x16_Error;
      }

      public static DgvEntry FromFailedChecksum(DirectoryInfo root, FileInfo file, string old, string current) => new DgvEntry(
        root,
        file,
        old,
        current);

      public static DgvEntry FromException(DirectoryInfo root, FileInfo file, string old, Exception exception) => new DgvEntry(
        root,
        file,
        old,
        exception);
    }

    #endregion

    private readonly SortableBindingList<DgvEntry> _entries = new SortableBindingList<DgvEntry>();
    private WindowStatus _currentStatus;

    private bool _verificationRunning;

    internal bool VerificationRunning {
      get { return this._verificationRunning; }
      set {
        this._verificationRunning = value;
        this._currentStatus = value ? new WindowStatus("Verification Running...") : WindowStatus.Empty;
        this.SafelyInvoke(new Action(() => this.tsmiVerifyFolders.Enabled = !value));
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

    internal void MarkFileChecksumFailed(FolderIntegrityChecker checker, FileInfo file, string oldChecksum, string newChecksum)
      => this.SafelyInvoke(() => this._AddEntry(DgvEntry.FromFailedChecksum(checker.RootDirectory, file, oldChecksum, newChecksum)));

    internal void MarkFileException(FolderIntegrityChecker checker, FileInfo file, string oldChecksum, Exception exception)
      => this.SafelyInvoke(() => this._AddEntry(DgvEntry.FromException(checker.RootDirectory, file, oldChecksum, exception)));

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

    private void tStatusTimer_Tick(object sender, EventArgs e) {
      var currentStatus = this._currentStatus;
      if (currentStatus.IsActionRunning)
        this.tsslCurrentStatus.Text = $"{currentStatus.Text}({currentStatus.RunTime:mm':'ss})";
      this.tsslCurrentStatus.Visible = currentStatus.IsActionRunning;
    }
  }
}