using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Filesystem_Toolbox.Core.Configuration;
using Filesystem_Toolbox.Core.Integrity;
using Filesystem_Toolbox.Core.Redundancy;
using Filesystem_Toolbox.Core.Services;

namespace Filesystem_Toolbox.Core {

  /// <summary>
  /// The application service: owns one context per watched folder - integrity checker,
  /// parity store, repair/mirror/refresh services - all assembled from the JSON configuration.
  /// </summary>
  public class ToolboxService : IDisposable {

    #region nested types

    private sealed class FolderContext : IDisposable {

      public FolderIntegrityChecker Checker { get; }

      /// <summary>The settings resolved at the watch root (parity percent is additionally resolved per file).</summary>
      public EffectiveSettings Effective { get; }

      public ParityStore ParityStore { get; }
      public BackupService Backup { get; }
      public RepairService Repair { get; }
      public RefreshService Refresh { get; }
      private readonly ParityMaintenanceQueue _maintenanceQueue;

      public FolderContext(FolderIntegrityChecker checker, EffectiveSettings effective, ConfigurationResolver resolver) {
        this.Checker = checker;
        this.Effective = effective;

        // the store always exists: deeper overrides may enable parity even when the root disables it,
        // and a per-file resolved percent of zero simply skips/deletes that file's parity
        this.ParityStore = new ParityStore(checker.RootDirectory, file => resolver.Resolve(file).ParityRedundancyPercent);
        this._maintenanceQueue = new ParityMaintenanceQueue(checker, this.ParityStore);

        if (!effective.BackupPath.IsNullOrWhiteSpace())
          this.Backup = new BackupService(
            checker,
            new DirectoryInfo(effective.BackupPath),
            new GfsRetentionPolicy(effective.GfsKeepDaily, effective.GfsKeepWeekly, effective.GfsKeepMonthly)
          );

        this.Repair = new RepairService(checker, this.ParityStore, this.Backup);

        if (effective.RefreshIntervalDays > 0)
          this.Refresh = new RefreshService(checker, TimeSpan.FromDays(effective.RefreshIntervalDays));
      }

      public void Dispose() {
        this._maintenanceQueue?.Dispose();
        this.Checker.Dispose();
      }

    }

    #endregion

    private static readonly DirectoryInfo _APPLICATION_FOLDER = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
    private const string _CONFIGURATION_FILE = "FilesystemToolbox.json";
    private const string _LEGACY_CONFIGURATION_FILE = "CheckedFolders.lst";
    private const string _SCHEDULER_STATE_FILE = "SchedulerState.json";

    private readonly List<FolderContext> _folders = new List<FolderContext>();
    private readonly Scheduling.SchedulerService _scheduler = new Scheduling.SchedulerService(_APPLICATION_FOLDER.File(_SCHEDULER_STATE_FILE));

    /// <summary>The append-only event history feeding the statistics.</summary>
    public Statistics.EventLog Events { get; } = new Statistics.EventLog(_APPLICATION_FOLDER.File("events.jsonl"));

    /// <summary>Raised when a root's monthly error count crosses its degradation threshold (once per day).</summary>
    public event Action<string> DeviceDegraded;

    /// <summary>Raised when a checksum database was found rotten while loading and a heal was attempted.</summary>
    public event Action<DirectoryInfo, DbHealResult> DatabaseHealed;

    private static FileInfo _ConfigurationFile => _APPLICATION_FOLDER.File(_CONFIGURATION_FILE);
    private static FileInfo _LegacyConfigurationFile => _APPLICATION_FOLDER.File(_LEGACY_CONFIGURATION_FILE);

    public ToolboxConfiguration Configuration { get; private set; } = new ToolboxConfiguration();

    /// <summary>Resolves effective settings for any path; rebuilt whenever the configuration changes.</summary>
    public ConfigurationResolver Resolver { get; private set; } = ConfigurationResolver.For(new ToolboxConfiguration());

    public void SaveConfiguration() => ConfigurationStore.Save(this.Configuration, _ConfigurationFile);

    public void LoadConfiguration() {
      this._ClearFolders();
      this.Configuration = ConfigurationStore.Load(_ConfigurationFile, _LegacyConfigurationFile);
      this._CreateFolders();
    }

    /// <summary>Applies a new configuration: persists it and re-creates all folder contexts.</summary>
    public void ApplyConfiguration(ToolboxConfiguration configuration) {
      if (configuration == null) throw new ArgumentNullException(nameof(configuration));

      this._ClearFolders();
      this.Configuration = configuration;
      this.SaveConfiguration();
      this._CreateFolders();
    }

    /// <summary>The settings effective at a checker's watch root.</summary>
    public EffectiveSettings GetEffectiveSettings(FolderIntegrityChecker checker)
      => this._FindContext(checker)?.Effective;

    public bool CanRepair(FolderIntegrityChecker checker) => this._FindContext(checker)?.Repair != null;

    public bool CanRestoreFromBackup(FolderIntegrityChecker checker) => this._FindContext(checker)?.Backup != null;

    public void RebuildDatabases() => this._ExecuteOnAllCheckers(c => c.RebuildDatabase());

    public void AcceptChange(FolderIntegrityChecker checker, FileInfo file) => checker.UpdateFile(file);

    /// <summary>Verifies all folders, reporting every non-Ok file with its classification.</summary>
    public void RunClassifiedChecks(Action<FolderIntegrityChecker, VerificationResult> onResult, CancellationToken token = default) {
      if (onResult == null) throw new ArgumentNullException(nameof(onResult));

      this._ExecuteOnAllContexts(context => this._VerifyContext(context, onResult, token));
    }

    /// <summary>Verifies a single watch root, reporting every non-Ok file with its classification.</summary>
    public void RunClassifiedChecks(string rootPath, Action<FolderIntegrityChecker, VerificationResult> onResult, CancellationToken token = default) {
      if (onResult == null) throw new ArgumentNullException(nameof(onResult));

      var context = this._FindContextByRoot(rootPath);
      if (context != null)
        this._VerifyContext(context, onResult, token);
    }

    private void _VerifyContext(FolderContext context, Action<FolderIntegrityChecker, VerificationResult> onResult, CancellationToken token) {
      var root = context.Checker.RootDirectory.FullName;
      var problemCounts = new Dictionary<VerificationStatus, int>();

      var verifier = new IntegrityVerifier(context.Checker, context.ParityStore);
      foreach (var result in verifier.VerifyAll(token)) {
        problemCounts.TryGetValue(result.Status, out var count);
        problemCounts[result.Status] = count + 1;

        if (result.Status == VerificationStatus.BitRot)
          this.Events.Append(Statistics.EventRecord.Now(root, Statistics.EventType.BitRotFound, result.File.FullName));

        onResult(context.Checker, result);
      }

      var record = Statistics.EventRecord.Now(
        root,
        Statistics.EventType.VerifyRun,
        detail: problemCounts.Count == 0 ? null : string.Join(";", problemCounts.Select(p => $"{p.Key}={p.Value}"))
      );
      record.FilesChecked = context.Checker.GetDatabaseSnapshot().Count;
      record.Problems = problemCounts.Values.Sum();
      this.Events.Append(record);

      // degradation watch: warn once per day when the monthly error budget is blown
      var statistics = new Statistics.StatisticsService(this.Events);
      if (statistics.CrossedThresholdToday(root, context.Effective.DegradationWarningErrorsPerMonth)) {
        this.Events.Append(Statistics.EventRecord.Now(root, Statistics.EventType.DeviceWarning));
        this.DeviceDegraded?.Invoke(root);
      }
    }

    /// <summary>Legacy callback-style verification, kept for the existing UI wiring.</summary>
    public void RunChecks(Action<FolderIntegrityChecker, FileInfo, string, string> onChecksumFailed, Action<FolderIntegrityChecker, FileInfo, string, Exception> onException)
      => this._ExecuteOnAllCheckers(c => c.VerifyIntegrity((f, o, n) => onChecksumFailed(c, f, o, n), (f, o, e) => onException(c, f, o, e)))
      ;

    /// <summary>Repairs one file using parity and/or backup; honest about the outcome.</summary>
    public RepairOutcome Repair(FolderIntegrityChecker checker, FileInfo file, CancellationToken token = default) {
      var context = this._FindContext(checker);
      if (context?.Repair == null)
        return new RepairOutcome(file, RepairResult.ParityMissing);

      var outcome = context.Repair.Repair(file, token);

      var root = checker.RootDirectory.FullName;
      switch (outcome.Result) {
        case RepairResult.Repaired:
          this.Events.Append(Statistics.EventRecord.Now(root, Statistics.EventType.Repaired, file.FullName));
          break;

        case RepairResult.RepairedFromBackup:
          this.Events.Append(Statistics.EventRecord.Now(root, Statistics.EventType.RepairedFromBackup, file.FullName));
          break;

        case RepairResult.Unrepairable:
        case RepairResult.ParityMissing:
          this.Events.Append(Statistics.EventRecord.Now(root, Statistics.EventType.Unrepairable, file.FullName));
          break;
      }

      return outcome;
    }

    /// <summary>Restores one file from the folder's backup snapshots (hash-verified, newest matching wins).</summary>
    public bool RestoreFromBackup(FolderIntegrityChecker checker, FileInfo file) {
      var context = this._FindContext(checker);
      if (context?.Backup == null || !checker.TryGetEntry(file, out var entry))
        return false;

      try {
        var restored = context.Backup.Restore(file, entry.Hash);
        if (restored)
          this.Events.Append(Statistics.EventRecord.Now(checker.RootDirectory.FullName, Statistics.EventType.RepairedFromBackup, file.FullName));

        return restored;
      } catch (IOException) {
        return false;
      } catch (UnauthorizedAccessException) {
        return false;
      }
    }

    /// <summary>Creates a GFS snapshot of one watch root; null when no backup is configured.</summary>
    public BackupReport RunBackup(string rootPath, CancellationToken token = default) {
      var context = this._FindContextByRoot(rootPath);
      var report = context?.Backup?.RunBackup(token);
      if (report != null) {
        var record = Statistics.EventRecord.Now(context.Checker.RootDirectory.FullName, Statistics.EventType.BackupRun, detail: report.SnapshotName);
        record.FilesChecked = report.FilesConsidered;
        record.Linked = report.Linked;
        record.Copied = report.Copied;
        this.Events.Append(record);
      }

      return report;
    }

    /// <summary>Creates GFS snapshots of every folder with a backup target; null when none has one.</summary>
    public List<BackupReport> RunBackupAll(CancellationToken token = default) {
      var reports = new List<BackupReport>();
      this._ExecuteOnAllContexts(context => {
        if (context.Backup == null)
          return;

        var report = this.RunBackup(context.Checker.RootDirectory.FullName, token);
        if (report != null)
          reports.Add(report);
      });

      return reports.Count > 0 ? reports : null;
    }

    /// <summary>Runs the effective on-corruption command for one file, if any is configured along the chain.</summary>
    public bool RunOnCorruptionCommand(FolderIntegrityChecker checker, FileInfo file) {
      var settings = this.GetEffectiveSettings(checker);
      return settings != null
        && Commands.OnCorruptionCommandRunner.Run(settings.OnCorruptionCommand, file, checker.RootDirectory)
        ;
    }

    /// <summary>Whether the folder's volume supports hard links (NTFS only).</summary>
    public static bool SupportsHardLinks(DirectoryInfo directory) {
      try {
        return string.Equals(new DriveInfo(directory.FullName).DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase);
      } catch (ArgumentException) {
        return false;
      } catch (IOException) {
        return false;
      }
    }

    /// <summary>
    /// Merges duplicate files of a watched folder into hard links (NTFS only, opt-in per folder).
    /// New links get the read-only attribute by default - NTFS hard links are not copy-on-write.
    /// </summary>
    public Dedup.DedupReport RunDedup(FolderIntegrityChecker checker, bool dryRun = false, Action<string> log = null) {
      var context = this._FindContext(checker);
      if (context == null || !context.Effective.DedupEnabled)
        return null;

      var root = checker.RootDirectory;
      if (!SupportsHardLinks(root))
        return null;

      var options = new Dedup.DedupOptions {
        ShowInfoOnly = dryRun,
        DirectoryFilter = d => !(
          string.Equals(d.Name, FolderIntegrityChecker.PROTECTED_FOLDER_NAME, StringComparison.OrdinalIgnoreCase)
          && string.Equals(d.Parent?.FullName, root.FullName, StringComparison.OrdinalIgnoreCase)
        ),
      };

      return Dedup.DuplicateFileMerger.ProcessFolders(new[] { root }, options, log);
    }

    /// <summary>Merges duplicates in every folder that has dedup enabled; returns the combined report.</summary>
    public Dedup.DedupReport RunDedupAll(bool dryRun = false, Action<string> log = null) {
      var total = new Dedup.DedupReport();
      var ranAtLeastOnce = false;

      this._ExecuteOnAllContexts(context => {
        var report = this.RunDedup(context.Checker, dryRun, log);
        if (report == null)
          return;

        ranAtLeastOnce = true;
        total.Merge(report);
      });

      return ranAtLeastOnce ? total : null;
    }

    /// <summary>Runs the preventive flash refresh on every folder that has it enabled.</summary>
    public RefreshReport RunRefresh(CancellationToken token = default) {
      var total = new RefreshReport();
      this._ExecuteOnAllContexts(context => {
        if (context.Refresh == null)
          return;

        var report = context.Refresh.RefreshDue(token);
        this._RecordRefresh(context, report);
        total.Refreshed += report.Refreshed;
        total.SkippedNotDue += report.SkippedNotDue;
        total.SkippedDirty += report.SkippedDirty;
        total.Errors += report.Errors;
      });

      return total;
    }

    private void _RecordRefresh(FolderContext context, RefreshReport report) {
      if (report.Refreshed == 0)
        return;

      var record = Statistics.EventRecord.Now(context.Checker.RootDirectory.FullName, Statistics.EventType.Refreshed);
      record.Count = report.Refreshed;
      this.Events.Append(record);
    }

    /// <summary>Scheduled actions (verify/backup/refresh per root) that are due right now.</summary>
    public IReadOnlyList<Scheduling.DueAction> GetDueActions() => this._scheduler.GetDueActions(this.Resolver);

    /// <summary>Claims a scheduled action so it cannot run twice concurrently.</summary>
    public bool TryBeginScheduled(Scheduling.DueAction action) => this._scheduler.TryBeginRun(action);

    /// <summary>Marks a scheduled action as successfully completed (persists its timestamp).</summary>
    public void CompleteScheduled(Scheduling.DueAction action) => this._scheduler.CompleteRun(action);

    /// <summary>Releases a failed scheduled action so it stays due and is retried.</summary>
    public void AbortScheduled(Scheduling.DueAction action) => this._scheduler.AbortRun(action);

    /// <summary>Runs the preventive flash refresh on a single watch root.</summary>
    public RefreshReport RunRefresh(string rootPath, CancellationToken token = default) {
      var context = this._FindContextByRoot(rootPath);
      if (context?.Refresh == null)
        return new RefreshReport();

      var report = context.Refresh.RefreshDue(token);
      this._RecordRefresh(context, report);
      return report;
    }

    private void _OnDatabaseHealed(DirectoryInfo root, DbHealResult result) {
      if (result == DbHealResult.Repaired)
        this.Events.Append(Statistics.EventRecord.Now(root.FullName, Statistics.EventType.DbRepaired));

      this.DatabaseHealed?.Invoke(root, result);
    }

    private FolderContext _FindContext(FolderIntegrityChecker checker) {
      lock (this._folders)
        return this._folders.FirstOrDefault(c => ReferenceEquals(c.Checker, checker));
    }

    private FolderContext _FindContextByRoot(string rootPath) {
      lock (this._folders)
        return this._folders.FirstOrDefault(c => string.Equals(c.Checker.RootDirectory.FullName, Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                                              || string.Equals(c.Checker.RootDirectory.FullName.TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase));
    }

    private void _ExecuteOnAllCheckers(Action<FolderIntegrityChecker> task) => this._ExecuteOnAllContexts(c => task(c.Checker));

    private void _ExecuteOnAllContexts(Action<FolderContext> task) {
      if (task == null) throw new ArgumentNullException(nameof(task));

      var alreadyRun = new HashSet<FolderContext>();
      while (true) {
        FolderContext current;
        lock (this._folders)
          current = this._folders.FirstOrDefault(c => !alreadyRun.Contains(c));

        if (current == null)
          return;

        alreadyRun.Add(current);
        task(current);
      }
    }

    private void _CreateFolders() {
      this.Resolver = ConfigurationResolver.For(this.Configuration);

      foreach (var folder in this.Resolver.WatchRoots) {
        var rootDirectory = new DirectoryInfo(folder.Path);
        if (rootDirectory.NotExists())
          continue;

        // subscribe BEFORE loading so a heal during the initial load is not missed
        var checker = new FolderIntegrityChecker(rootDirectory);
        checker.DatabaseHealed += this._OnDatabaseHealed;
        checker.LoadDatabase();

        var context = new FolderContext(checker, this.Resolver.Resolve(rootDirectory), this.Resolver);
        lock (this._folders)
          this._folders.Add(context);

        checker.Enabled = true;
      }
    }

    private void _ClearFolders() {
      FolderContext[] contexts;

      lock (this._folders) {
        contexts = this._folders.ToArray();
        this._folders.Clear();
      }

      foreach (var context in contexts)
        context.Dispose();
    }

    #region IDisposable

    private int _isDisposed;
    public bool IsDisposed => this._isDisposed != 0;

    private void _ReleaseUnmanagedResources() {
      if (Interlocked.CompareExchange(ref this._isDisposed, 1, 0) != 0)
        return;

      this._ClearFolders();
    }

    public void Dispose() {
      this._ReleaseUnmanagedResources();
      GC.SuppressFinalize(this);
    }

    ~ToolboxService() {
      this._ReleaseUnmanagedResources();
    }

    #endregion

  }
}
