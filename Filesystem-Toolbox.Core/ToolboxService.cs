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
      public WatchedFolderConfiguration Configuration { get; }
      public ParityStore ParityStore { get; }
      public MirrorService Mirror { get; }
      public RepairService Repair { get; }
      public RefreshService Refresh { get; }
      private readonly ParityMaintenanceQueue _maintenanceQueue;

      public FolderContext(WatchedFolderConfiguration configuration, FolderIntegrityChecker checker) {
        this.Configuration = configuration;
        this.Checker = checker;

        if (configuration.ParityRedundancyPercent > 0) {
          this.ParityStore = new ParityStore(checker.RootDirectory, configuration.ParityRedundancyPercent);
          this._maintenanceQueue = new ParityMaintenanceQueue(checker, this.ParityStore);
        }

        if (!configuration.MirrorPath.IsNullOrWhiteSpace())
          this.Mirror = new MirrorService(checker.RootDirectory, new DirectoryInfo(configuration.MirrorPath));

        if (this.ParityStore != null)
          this.Repair = new RepairService(checker, this.ParityStore, this.Mirror);

        if (configuration.RefreshIntervalDays > 0)
          this.Refresh = new RefreshService(checker, TimeSpan.FromDays(configuration.RefreshIntervalDays));
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

    private readonly List<FolderContext> _folders = new List<FolderContext>();

    private static FileInfo _ConfigurationFile => _APPLICATION_FOLDER.File(_CONFIGURATION_FILE);
    private static FileInfo _LegacyConfigurationFile => _APPLICATION_FOLDER.File(_LEGACY_CONFIGURATION_FILE);

    public ToolboxConfiguration Configuration { get; private set; } = new ToolboxConfiguration();

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

    public WatchedFolderConfiguration GetFolderConfiguration(FolderIntegrityChecker checker)
      => this._FindContext(checker)?.Configuration;

    public bool CanRepair(FolderIntegrityChecker checker) => this._FindContext(checker)?.Repair != null;

    public bool HasMirror(FolderIntegrityChecker checker) => this._FindContext(checker)?.Mirror != null;

    public void RebuildDatabases() => this._ExecuteOnAllCheckers(c => c.RebuildDatabase());

    public void AcceptChange(FolderIntegrityChecker checker, FileInfo file) => checker.UpdateFile(file);

    /// <summary>Verifies all folders, reporting every non-Ok file with its classification.</summary>
    public void RunClassifiedChecks(Action<FolderIntegrityChecker, VerificationResult> onResult, CancellationToken token = default) {
      if (onResult == null) throw new ArgumentNullException(nameof(onResult));

      this._ExecuteOnAllContexts(context => {
        var verifier = new IntegrityVerifier(context.Checker, context.ParityStore);
        foreach (var result in verifier.VerifyAll(token))
          onResult(context.Checker, result);
      });
    }

    /// <summary>Legacy callback-style verification, kept for the existing UI wiring.</summary>
    public void RunChecks(Action<FolderIntegrityChecker, FileInfo, string, string> onChecksumFailed, Action<FolderIntegrityChecker, FileInfo, string, Exception> onException)
      => this._ExecuteOnAllCheckers(c => c.VerifyIntegrity((f, o, n) => onChecksumFailed(c, f, o, n), (f, o, e) => onException(c, f, o, e)))
      ;

    /// <summary>Repairs one file using parity and/or mirror; honest about the outcome.</summary>
    public RepairOutcome Repair(FolderIntegrityChecker checker, FileInfo file, CancellationToken token = default) {
      var context = this._FindContext(checker);
      if (context?.Repair == null)
        return new RepairOutcome(file, RepairResult.ParityMissing);

      return context.Repair.Repair(file, token);
    }

    /// <summary>Restores one file from the folder's mirror (hash-verified); <c>false</c> when no usable copy exists.</summary>
    public bool RestoreFromMirror(FolderIntegrityChecker checker, FileInfo file) {
      var context = this._FindContext(checker);
      if (context?.Mirror == null || !checker.TryGetEntry(file, out var entry))
        return false;

      try {
        return context.Mirror.Restore(file, entry.Hash);
      } catch (IOException) {
        return false;
      } catch (UnauthorizedAccessException) {
        return false;
      }
    }

    /// <summary>Pushes verified-good files of all mirrored folders into their mirrors.</summary>
    public void SyncMirrors(CancellationToken token = default) => this._ExecuteOnAllContexts(context => {
      if (context.Mirror == null)
        return;

      foreach (var pair in context.Checker.GetDatabaseSnapshot()) {
        token.ThrowIfCancellationRequested();

        if (!ChecksumEntry.TryParse(pair.Value, out var stored))
          continue;

        var file = context.Checker.GetFile(pair.Key);
        try {
          if (ChecksumEntry.FromFile(file).ContentEquals(stored))
            context.Mirror.Sync(file, token);
        } catch (IOException) {
          ;
        } catch (UnauthorizedAccessException) {
          ;
        }
      }
    });

    /// <summary>Runs the folder's configured on-corruption command for one file, if any is set.</summary>
    public bool RunOnCorruptionCommand(FolderIntegrityChecker checker, FileInfo file) {
      var configuration = this.GetFolderConfiguration(checker);
      return configuration != null
        && Commands.OnCorruptionCommandRunner.Run(configuration.OnCorruptionCommand, file, checker.RootDirectory)
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
      if (context == null || !context.Configuration.DedupEnabled)
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
        total.Refreshed += report.Refreshed;
        total.SkippedNotDue += report.SkippedNotDue;
        total.SkippedDirty += report.SkippedDirty;
        total.Errors += report.Errors;
      });

      return total;
    }

    private FolderContext _FindContext(FolderIntegrityChecker checker) {
      lock (this._folders)
        return this._folders.FirstOrDefault(c => ReferenceEquals(c.Checker, checker));
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
      foreach (var folder in this.Configuration.Folders) {
        if (folder.Path.IsNullOrWhiteSpace())
          continue;

        var rootDirectory = new DirectoryInfo(folder.Path);
        if (rootDirectory.NotExists())
          continue;

        var checker = FolderIntegrityChecker.Create(rootDirectory);
        var context = new FolderContext(folder, checker);
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
