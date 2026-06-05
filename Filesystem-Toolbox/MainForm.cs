using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Filesystem_Toolbox.Core;
using Filesystem_Toolbox.Core.Integrity;
using Filesystem_Toolbox.Core.Services;

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
      private readonly FileInfo _file;

      [Browsable(false)]
      public FolderIntegrityChecker Checker { get; }

      [Browsable(false)]
      public FileInfo File => this._file;

      [DataGridViewColumnWidth(24)]
      public Image Image { get; }
      public string Status { get; }
      public string FileName => this._file.Name;
      public string Extension => this._file.Extension;
      public string Name => this._file.GetFilenameWithoutExtension();
      public string FolderName => this._file.Directory?.Name;
      public string RelativePath => this._file.Directory?.RelativeTo(this.Checker.RootDirectory);
      public string Path => this._file.Directory?.FullName;
      public string Checksum { get; }
      public string OldChecksum { get; }
      public string Exception { get; }

      private DgvEntry(FolderIntegrityChecker checker, FileInfo file, string status, string oldChecksum, string currentChecksum, string exception, Image image) {
        this._file = file;
        this.Checker = checker;
        this.Status = status;
        this.OldChecksum = oldChecksum;
        this.Checksum = currentChecksum;
        this.Exception = exception;
        this.Image = image;
      }

      public static DgvEntry FromResult(FolderIntegrityChecker checker, VerificationResult result) => new DgvEntry(
        checker,
        result.File,
        result.Status.ToString(),
        result.StoredEntry?.ToString(),
        result.ActualEntry?.ToString(),
        result.Error?.Message,
        result.Status switch {
          VerificationStatus.Missing or VerificationStatus.Error => Properties.Resources._16x16_Error,
          _ => Properties.Resources._16x16_Warning,
        }
      );

      public static DgvEntry FromRepair(FolderIntegrityChecker checker, RepairOutcome outcome) => new DgvEntry(
        checker,
        outcome.File,
        outcome.Result == RepairResult.RepairedFromMirror ? "Restored (auto)" : "Repaired (auto)",
        null,
        null,
        outcome.Error?.Message,
        Properties.Resources.tick_small
      );
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

    private bool _rebuildRunning;

    internal bool RebuildRunning {
      get { return this._rebuildRunning; }
      set {
        this._rebuildRunning = value;
        this._currentStatus = value ? new WindowStatus("Rebuild Running...") : WindowStatus.Empty;
        this.SafelyInvoke(new Action(() => this.tsmiRebuildDatabase.Enabled = !value));
      }
    }

    private readonly ToolboxService _logic;
    private readonly System.Threading.Timer _checkTimer;

    private TimeSpan _CheckInterval => TimeSpan.FromMinutes(this._logic?.Configuration?.CheckIntervalMinutes ?? 10);

    internal MainForm(ToolboxService logic = null) {
      this._logic = logic;
      this.InitializeComponent();
      this.SetFormTitle();

      this.dgvProblems.DataSource = this._entries;
      this._checkTimer = new System.Threading.Timer(this.tCheckTimer_Tick);
      this._checkTimer.Change(this._CheckInterval, Timeout.InfiniteTimeSpan);
    }

    private void _AddEntry(DgvEntry entry) {
      if (entry == null)
        throw new ArgumentNullException(nameof(entry));

      var entries = this._entries;
      for (var i = entries.Count - 1; i >= 0; --i)
        if (entries[i].File.FullName == entry.File.FullName)
          entries.RemoveAt(i);

      entries.Add(entry);
    }

    private void _RemoveEntriesForFile(FileInfo file) => this.SafelyInvoke(() => {
      var entries = this._entries;
      for (var i = entries.Count - 1; i >= 0; --i)
        if (entries[i].File.FullName == file.FullName)
          entries.RemoveAt(i);
    });

    /// <summary>
    /// Handles one classified verification result: auto-repairs where allowed, keeps parity
    /// bindings fresh, runs the configured on-corruption command for everything that stays
    /// broken, and surfaces the rest in the grid.
    /// </summary>
    private void _ProcessVerificationResult(FolderIntegrityChecker checker, VerificationResult result) {
      var configuration = this._logic?.GetFolderConfiguration(checker);

      switch (result.Status) {
        case VerificationStatus.ParityStale:

          // safe metadata work - rebuild the parity binding silently
          if (this._logic?.CanRepair(checker) == true)
            this._logic.Repair(checker, result.File);

          return;

        case VerificationStatus.BitRot:
        case VerificationStatus.Missing:
        case VerificationStatus.Error:
          if (configuration?.AutoRepair == true && this._logic?.CanRepair(checker) == true) {
            var outcome = this._logic.Repair(checker, result.File);
            if (outcome.Result is RepairResult.Repaired or RepairResult.RepairedFromMirror) {
              this.SafelyInvoke(() => this._AddEntry(DgvEntry.FromRepair(checker, outcome)));
              return;
            }
          }

          // still broken - notify the configured command, then show it
          if (!string.IsNullOrWhiteSpace(configuration?.OnCorruptionCommand))
            this._logic?.RunOnCorruptionCommand(checker, result.File);

          break;
      }

      this.SafelyInvoke(() => this._AddEntry(DgvEntry.FromResult(checker, result)));
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

    private void tCheckTimer_Tick(object _) {
      this._checkTimer.Change(Timeout.Infinite, Timeout.Infinite);
      var isRunning = (bool?)null;
      try {
        isRunning = this.VerificationRunning;
        if (isRunning.Value)
          return;

        this.VerificationRunning = true;
        this._logic?.RunClassifiedChecks(this._ProcessVerificationResult);
      } finally {
        if (isRunning != null && !isRunning.Value)
          this.VerificationRunning = false;

        this._checkTimer.Change(this._CheckInterval, Timeout.InfiniteTimeSpan);
      }
    }

    private void tsmiVerifyFolders_Click(object _, EventArgs __) => this.Async(this.tCheckTimer_Tick);

    private void tsmiRebuildDatabase_Click(object _, EventArgs __) {
      if (
        MessageBox.Show(
          "This will reset all checksum and rebuild the whole database.\r\nAre you sure?",
          "Rebuild Database",
          MessageBoxButtons.YesNo,
          MessageBoxIcon.Question) != DialogResult.Yes)
        return;

      this.Async(
        () => {
          var isRunning = (bool?)null;
          try {
            isRunning = this.RebuildRunning;
            if (isRunning.Value)
              return;

            this.RebuildRunning = true;
            this._logic?.RebuildDatabases();
          } finally {
            if (isRunning != null && !isRunning.Value)
              this.RebuildRunning = false;

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

    private void tsmiAcceptDifference_Click(object sender, EventArgs e) {
      foreach (var item in this.dgvProblems.GetSelectedItems<DgvEntry>().ToArray()) {
        this._logic.AcceptChange(item.Checker, item.File);
        this._entries.Remove(item);
      }
    }

    private void tsmiRepair_Click(object sender, EventArgs e) {
      var items = this.dgvProblems.GetSelectedItems<DgvEntry>().ToArray();
      this.Async(() => {
        this._currentStatus = new WindowStatus("Repair Running...");
        try {
          foreach (var item in items) {
            var outcome = this._logic.Repair(item.Checker, item.File);
            switch (outcome.Result) {
              case RepairResult.Repaired:
              case RepairResult.RepairedFromMirror:
              case RepairResult.ParityRebuilt:
              case RepairResult.NotNeeded:
                this._RemoveEntriesForFile(item.File);
                break;

              default:
                this.SafelyInvoke(() => MessageBox.Show(
                  this,
                  $"{item.File.Name}: {outcome.Result}{(outcome.Error == null ? string.Empty : $" - {outcome.Error.Message}")}",
                  "Repair",
                  MessageBoxButtons.OK,
                  MessageBoxIcon.Warning
                ));
                break;
            }
          }
        } finally {
          this._currentStatus = WindowStatus.Empty;
        }
      });
    }

    private void tsmiRestoreFromMirror_Click(object sender, EventArgs e) {
      var items = this.dgvProblems.GetSelectedItems<DgvEntry>().ToArray();
      this.Async(() => {
        this._currentStatus = new WindowStatus("Mirror Restore Running...");
        try {
          foreach (var item in items)
            if (this._logic.RestoreFromMirror(item.Checker, item.File))
              this._RemoveEntriesForFile(item.File);
            else
              this.SafelyInvoke(() => MessageBox.Show(
                this,
                $"{item.File.Name}: no usable mirror copy (missing or checksum mismatch)",
                "Restore from mirror",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
              ));
        } finally {
          this._currentStatus = WindowStatus.Empty;
        }
      });
    }

    private void tsmiRunCommand_Click(object sender, EventArgs e) {
      foreach (var item in this.dgvProblems.GetSelectedItems<DgvEntry>().ToArray())
        this._logic.RunOnCorruptionCommand(item.Checker, item.File);
    }

    private void tsmiSettings_Click(object sender, EventArgs e) {
      if (this._logic == null)
        return;

      using (var dialog = new SettingsForm(this._logic.Configuration)) {
        if (dialog.ShowDialog(this) != DialogResult.OK)
          return;

        this._logic.ApplyConfiguration(dialog.Result);
        this._checkTimer.Change(this._CheckInterval, Timeout.InfiniteTimeSpan);
      }
    }

    private void tsmiRunDedup_Click(object sender, EventArgs e) => this.Async(() => {
      this._currentStatus = new WindowStatus("Duplicate Merge Running...");
      try {
        var report = this._logic?.RunDedupAll();
        this.SafelyInvoke(() => MessageBox.Show(
          this,
          report == null
            ? "No folder has duplicate merging enabled (or none is on an NTFS volume)."
            : $"Scanned {report.FilesScanned} files, created {report.HardLinksCreated} hard links and {report.SymbolicLinksCreated} symbolic links ({report.Errors} errors).",
          "Merge duplicates",
          MessageBoxButtons.OK,
          MessageBoxIcon.Information
        ));
      } finally {
        this._currentStatus = WindowStatus.Empty;
      }
    });

    private void tsmiRunRefresh_Click(object sender, EventArgs e) => this.Async(() => {
      this._currentStatus = new WindowStatus("Media Refresh Running...");
      try {
        var report = this._logic?.RunRefresh();
        this.SafelyInvoke(() => MessageBox.Show(
          this,
          report == null
            ? "Nothing to refresh."
            : $"Refreshed {report.Refreshed} files ({report.SkippedNotDue} not due, {report.SkippedDirty} skipped because they do not verify clean, {report.Errors} errors).",
          "Refresh media",
          MessageBoxButtons.OK,
          MessageBoxIcon.Information
        ));
      } finally {
        this._currentStatus = WindowStatus.Empty;
      }
    });

    private void tsmiSyncMirrors_Click(object sender, EventArgs e) => this.Async(() => {
      this._currentStatus = new WindowStatus("Mirror Sync Running...");
      try {
        this._logic?.SyncMirrors();
        this.SafelyInvoke(() => MessageBox.Show(
          this,
          "Verified-good files of all mirrored folders were copied into their mirrors.",
          "Sync mirrors",
          MessageBoxButtons.OK,
          MessageBoxIcon.Information
        ));
      } finally {
        this._currentStatus = WindowStatus.Empty;
      }
    });

    private void cmsItems_Opening(object sender, CancelEventArgs e) {
      var selected = this.dgvProblems.GetSelectedItems<DgvEntry>().ToArray();
      if (!selected.Any()) {
        e.Cancel = true;
        return;
      }

      var logic = this._logic;
      this.tsmiRepair.Enabled = selected.Any(i => logic?.CanRepair(i.Checker) == true);
      this.tsmiRestoreFromMirror.Enabled = selected.Any(i => logic?.HasMirror(i.Checker) == true);
      this.tsmiRunCommand.Enabled = selected.Any(i => !string.IsNullOrWhiteSpace(logic?.GetFolderConfiguration(i.Checker)?.OnCorruptionCommand));
    }

  }
}
