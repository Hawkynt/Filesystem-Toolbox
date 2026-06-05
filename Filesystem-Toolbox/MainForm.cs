using System;
using System.Collections.Generic;
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
        outcome.Result == RepairResult.RepairedFromBackup ? "Restored (auto)" : "Repaired (auto)",
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
    private readonly INotifier _notifier;
    private readonly System.Threading.Timer _schedulerTimer;
    private static readonly TimeSpan _SCHEDULER_POLL = TimeSpan.FromMinutes(1);

    internal MainForm(ToolboxService logic = null, INotifier notifier = null) {
      this._logic = logic;
      this._notifier = notifier;
      this.InitializeComponent();
      this.SetFormTitle();

      this.dgvProblems.DataSource = this._entries;
      this._schedulerTimer = new System.Threading.Timer(this._SchedulerTick);
      this._schedulerTimer.Change(TimeSpan.FromSeconds(5), _SCHEDULER_POLL);

      if (logic != null) {
        logic.DeviceDegraded += root => this._Notify(root, n => n.Warning("Device degrading", $"{root} exceeded its monthly error budget - check the statistics."));
        logic.DatabaseHealed += (root, result) => {
          switch (result) {
            case Core.Integrity.DbHealResult.Repaired:
              this._Notify(root.FullName, n => n.Warning("Database repaired", $"The checksum database of {root.FullName} had rotted and was healed from its parity."));
              break;

            case Core.Integrity.DbHealResult.Unrepairable:
              this._Notify(root.FullName, n => n.Error("Database damaged", $"The checksum database of {root.FullName} is damaged beyond repair - a rebuild is advised."));
              break;
          }
        };
      }
    }

    /// <summary>Routes a toast through the per-root ToastNotifications switch.</summary>
    private void _Notify(string rootPath, Action<INotifier> send) {
      if (this._notifier == null)
        return;

      if (rootPath != null && this._logic?.Resolver.Resolve(rootPath).ToastNotifications == false)
        return;

      send(this._notifier);
    }

    /// <summary>
    /// The 1-minute scheduler poll: asks the service which per-root actions are due
    /// (per the inherited schedules, including catch-ups after downtime) and dispatches
    /// each one once - claims prevent double-runs, failures stay due and are retried.
    /// </summary>
    private void _SchedulerTick(object _) {
      var logic = this._logic;
      if (logic == null)
        return;

      foreach (var due in logic.GetDueActions()) {
        if (!logic.TryBeginScheduled(due))
          continue;

        var action = due;
        this.Async(() => {
          try {
            switch (action.Action) {
              case Core.Scheduling.ScheduledAction.Verify:
                this._currentStatus = new WindowStatus("Verification Running...");
                logic.RunClassifiedChecks(action.RootPath, this._ProcessVerificationResult);
                break;

              case Core.Scheduling.ScheduledAction.Backup:
                this._currentStatus = new WindowStatus("Backup Running...");
                logic.RunBackup(action.RootPath);
                break;

              case Core.Scheduling.ScheduledAction.Refresh:
                this._currentStatus = new WindowStatus("Media Refresh Running...");
                logic.RunRefresh(action.RootPath);
                break;
            }

            logic.CompleteScheduled(action);
          } catch (Exception) {
            logic.AbortScheduled(action);
          } finally {
            this._currentStatus = WindowStatus.Empty;
          }
        });
      }
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
      var configuration = this._logic?.GetEffectiveSettings(checker);
      var rootPath = checker.RootDirectory.FullName;

      switch (result.Status) {
        case VerificationStatus.ParityStale:

          // safe metadata work - rebuild the parity binding silently
          if (this._logic?.CanRepair(checker) == true)
            this._logic.Repair(checker, result.File);

          return;

        case VerificationStatus.BitRot:
        case VerificationStatus.Missing:
        case VerificationStatus.Error:
          var autoRepairAttempted = false;
          if (configuration?.AutoRepair == true && this._logic?.CanRepair(checker) == true) {
            autoRepairAttempted = true;
            var outcome = this._logic.Repair(checker, result.File);
            if (outcome.Result is RepairResult.Repaired or RepairResult.RepairedFromBackup) {

              // a defect happened to the medium even though it was healed - warn, per the workflow
              this._Notify(rootPath, n => n.Warning("Repaired", $"{result.File.Name} had {result.Status} and was repaired automatically."));
              this.SafelyInvoke(() => this._AddEntry(DgvEntry.FromRepair(checker, outcome)));
              return;
            }
          }

          // still broken - notify the configured command, toast, and offer choices when repair already failed
          if (!string.IsNullOrWhiteSpace(configuration?.OnCorruptionCommand))
            this._logic?.RunOnCorruptionCommand(checker, result.File);

          if (autoRepairAttempted) {
            this._Notify(rootPath, n => n.Error("Unrepairable", $"{result.File.Name} could not be restored."));
            this._EnqueueUnrepairable(checker, result.File);
          } else
            this._Notify(rootPath, n => n.Warning("Integrity problem", $"{result.File.Name}: {result.Status}"));

          break;
      }

      this.SafelyInvoke(() => this._AddEntry(DgvEntry.FromResult(checker, result)));
    }

    #region unrepairable dialog queue

    private readonly Queue<(FolderIntegrityChecker Checker, FileInfo File)> _unrepairableQueue = new Queue<(FolderIntegrityChecker, FileInfo)>();
    private bool _dialogShowing;
    private UnrepairableChoice? _applyToAllChoice;

    private void _EnqueueUnrepairable(FolderIntegrityChecker checker, FileInfo file) => this.SafelyInvoke(() => {
      this._unrepairableQueue.Enqueue((checker, file));
      this._PumpUnrepairable();
    });

    /// <summary>Shows queued dialogs one at a time (UI thread); "apply to all" short-circuits the rest.</summary>
    private void _PumpUnrepairable() {
      if (this._dialogShowing)
        return;

      while (this._unrepairableQueue.Count > 0) {
        var (checker, file) = this._unrepairableQueue.Dequeue();

        var choice = this._applyToAllChoice;
        if (choice == null) {
          this._dialogShowing = true;
          try {
            using (var dialog = new UnrepairableFileForm(file, this._logic?.CanRestoreFromBackup(checker) == true)) {
              dialog.ShowDialog(this);
              choice = dialog.Choice;
              if (dialog.ApplyToAll)
                this._applyToAllChoice = choice;
            }
          } finally {
            this._dialogShowing = false;
          }
        }

        this._ApplyUnrepairableChoice(checker, file, choice.Value);
      }

      this._applyToAllChoice = null; // each batch asks anew
    }

    private void _ApplyUnrepairableChoice(FolderIntegrityChecker checker, FileInfo file, UnrepairableChoice choice) {
      try {
        switch (choice) {
          case UnrepairableChoice.RestoreFromBackup:
            if (this._logic?.RestoreFromBackup(checker, file) == true)
              this._RemoveEntriesForFile(file);
            else
              this._Notify(checker.RootDirectory.FullName, n => n.Error("Restore failed", $"No backup snapshot holds the recorded content of {file.Name}."));

            break;

          case UnrepairableChoice.Rename: {
            var originalPath = file.FullName;
            file.Refresh();
            if (file.Exists) {
              file.Attributes &= ~FileAttributes.ReadOnly;
              file.MoveTo(originalPath + ".corrupt");
            }

            var original = new FileInfo(originalPath);
            checker.UpdateFile(original); // gone from its old name -> the entry is dropped
            this._RemoveEntriesForFile(original);
            break;
          }

          case UnrepairableChoice.Delete:
            file.Refresh();
            if (file.Exists) {
              file.Attributes &= ~FileAttributes.ReadOnly;
              file.Delete();
            }

            checker.UpdateFile(file);
            this._RemoveEntriesForFile(file);
            break;
        }
      } catch (IOException) {
        ;
      } catch (UnauthorizedAccessException) {
        ;
      }
    }

    #endregion

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

    /// <summary>Manual full verification of every folder (the scheduler runs per-root checks on its own).</summary>
    private void _RunAllChecks() {
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
      }
    }

    private void tsmiVerifyFolders_Click(object _, EventArgs __) => this.Async(this._RunAllChecks);

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
              case RepairResult.RepairedFromBackup:
              case RepairResult.ParityRebuilt:
              case RepairResult.NotNeeded:
                this._RemoveEntriesForFile(item.File);
                break;

              case RepairResult.ModifiedNotRepaired:
                this.SafelyInvoke(() => MessageBox.Show(
                  this,
                  $"{item.File.Name} was intentionally edited - accept the change instead of repairing.",
                  "Repair",
                  MessageBoxButtons.OK,
                  MessageBoxIcon.Information
                ));
                break;

              default:

                // repair failed - error toast and the what-now dialog, as configured
                this._Notify(item.Checker.RootDirectory.FullName, n => n.Error("Unrepairable", $"{item.File.Name} could not be restored."));
                this._EnqueueUnrepairable(item.Checker, item.File);
                break;
            }
          }
        } finally {
          this._currentStatus = WindowStatus.Empty;
        }
      });
    }

    private void tsmiRestoreFromBackup_Click(object sender, EventArgs e) {
      var items = this.dgvProblems.GetSelectedItems<DgvEntry>().ToArray();
      this.Async(() => {
        this._currentStatus = new WindowStatus("Backup Restore Running...");
        try {
          foreach (var item in items)
            if (this._logic.RestoreFromBackup(item.Checker, item.File))
              this._RemoveEntriesForFile(item.File);
            else
              this.SafelyInvoke(() => MessageBox.Show(
                this,
                $"{item.File.Name}: no backup snapshot holds this content (missing or rotted)",
                "Restore from backup",
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

        this._logic.ApplyConfiguration(dialog.Result); // the scheduler poll picks up new schedules on its next tick
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

    private void tsmiRunBackup_Click(object sender, EventArgs e) => this.Async(() => {
      this._currentStatus = new WindowStatus("Backup Running...");
      try {
        var reports = this._logic?.RunBackupAll();
        this.SafelyInvoke(() => MessageBox.Show(
          this,
          reports == null
            ? "No folder has a backup target configured."
            : string.Join(
                Environment.NewLine,
                reports.Select(r => $"{r.SnapshotName}: {r.Copied} copied, {r.Linked} linked, {r.SkippedDirty} skipped dirty, {r.Errors} errors, {r.SnapshotsPruned} pruned")
              ),
          "Backup",
          MessageBoxButtons.OK,
          MessageBoxIcon.Information
        ));
      } finally {
        this._currentStatus = WindowStatus.Empty;
      }
    });

    private void tsmiStatistics_Click(object sender, EventArgs e) {
      if (this._logic == null)
        return;

      using (var dialog = new StatisticsForm(this._logic))
        dialog.ShowDialog(this);
    }

    private void cmsItems_Opening(object sender, CancelEventArgs e) {
      var selected = this.dgvProblems.GetSelectedItems<DgvEntry>().ToArray();
      if (!selected.Any()) {
        e.Cancel = true;
        return;
      }

      var logic = this._logic;
      this.tsmiRepair.Enabled = selected.Any(i => logic?.CanRepair(i.Checker) == true);
      this.tsmiRestoreFromBackup.Enabled = selected.Any(i => logic?.CanRestoreFromBackup(i.Checker) == true);
      this.tsmiRunCommand.Enabled = selected.Any(i => !string.IsNullOrWhiteSpace(logic?.GetEffectiveSettings(i.Checker)?.OnCorruptionCommand));
    }

  }
}
