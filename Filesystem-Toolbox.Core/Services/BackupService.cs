using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Filesystem_Toolbox.Core.Integrity;

namespace Filesystem_Toolbox.Core.Services {

  public sealed class BackupReport {

    public int FilesConsidered { get; internal set; }
    public int Copied { get; internal set; }
    public int Linked { get; internal set; }

    /// <summary>Files whose content no longer matches the database - rot is never backed up.</summary>
    public int SkippedDirty { get; internal set; }

    public int Errors { get; internal set; }
    public int SnapshotsPruned { get; internal set; }
    public string SnapshotName { get; internal set; }

    /// <summary>Hard-linking against the previous snapshot was unavailable; full copies were made.</summary>
    public bool FellBackToCopy { get; internal set; }

  }

  /// <summary>
  /// Versioned, deduplicated backups with grandfather-father-son retention: every run creates
  /// a snapshot folder <c>&lt;backupRoot&gt;/yyyy-MM-dd_HHmmss/&lt;relpath&gt;</c>; files unchanged
  /// since the previous snapshot become hard links (NTFS) instead of copies, so history is
  /// cheap. Only verified-clean files enter a snapshot. Restores search snapshots newest to
  /// oldest for content matching the recorded hash - even an older snapshot can supply the
  /// wanted version, and a rotted backup copy is detected and skipped.
  /// </summary>
  public sealed class BackupService {

    private const string _PARTIAL_SUFFIX = ".partial";
    private const string _SNAPSHOT_NAME_FORMAT = "yyyy-MM-dd_HHmmss";

    private readonly FolderIntegrityChecker _checker;
    private readonly GfsRetentionPolicy _retention;
    private readonly Action<string> _log;

    public DirectoryInfo Root { get; }
    public DirectoryInfo BackupRoot { get; }

    public BackupService(FolderIntegrityChecker checker, DirectoryInfo backupRoot, GfsRetentionPolicy retention, Action<string> log = null) {
      this._checker = checker ?? throw new ArgumentNullException(nameof(checker));
      this.BackupRoot = backupRoot ?? throw new ArgumentNullException(nameof(backupRoot));
      this.Root = checker.RootDirectory;
      this._retention = retention;
      this._log = log;
    }

    /// <summary>Creates one snapshot (verify-clean, link-or-copy, manifest, atomic publish) and prunes per GFS.</summary>
    public BackupReport RunBackup(CancellationToken token = default) {
      var report = new BackupReport();
      this.BackupRoot.Create();
      this._ReclaimLeftovers();

      var previous = this.LatestSnapshot();
      var previousManifest = previous == null ? new SnapshotManifest() : SnapshotManifest.Load(previous);

      var name = this._UniqueSnapshotName(DateTime.UtcNow);
      report.SnapshotName = name;
      var staging = new DirectoryInfo(Path.Combine(this.BackupRoot.FullName, name + _PARTIAL_SUFFIX));
      staging.Create();

      var linkingAvailable = ToolboxService.SupportsHardLinks(this.BackupRoot);
      var manifest = new SnapshotManifest();

      foreach (var pair in this._checker.GetDatabaseSnapshot()) {
        token.ThrowIfCancellationRequested();
        ++report.FilesConsidered;

        if (!ChecksumEntry.TryParse(pair.Value, out var stored)) {
          ++report.Errors;
          continue;
        }

        var file = this._checker.GetFile(pair.Key);
        file.Refresh();
        if (!file.Exists) {
          ++report.Errors;
          continue;
        }

        // never back up rot - a snapshot full of corruption would poison every restore
        ChecksumEntry actual;
        try {
          actual = ChecksumEntry.FromFile(file);
        } catch (Exception) {
          ++report.Errors;
          continue;
        }

        if (!actual.ContentEquals(stored)) {
          ++report.SkippedDirty;
          continue;
        }

        var target = new FileInfo(Path.Combine(staging.FullName, pair.Key));
        target.Directory.Create();

        try {
          var unchanged = previousManifest.TryGet(pair.Key, out var previousEntry) && previousEntry.ContentEquals(stored);
          var previousFile = unchanged ? new FileInfo(Path.Combine(previous.FullName, pair.Key)) : null;

          if (unchanged && linkingAvailable && previousFile.Exists) {
            try {
              target.CreateHardLinkFrom(previousFile.FullName);
              ++report.Linked;
            } catch (Exception) {
              this._WarnLinkFallback(report);
              file.CopyTo(target.FullName, true);
              ++report.Copied;
            }
          } else {
            if (unchanged && !linkingAvailable)
              this._WarnLinkFallback(report);

            file.CopyTo(target.FullName, true);
            ++report.Copied;
          }

          manifest.Set(pair.Key, stored);
        } catch (IOException) {
          ++report.Errors;
        } catch (UnauthorizedAccessException) {
          ++report.Errors;
        }
      }

      manifest.Save(staging);

      // atomic publish: a crash before this point leaves only an ignored *.partial directory
      Directory.Move(staging.FullName, Path.Combine(this.BackupRoot.FullName, name));

      report.SnapshotsPruned = this._Prune();
      return report;
    }

    /// <summary>
    /// Restores a file whose content must match <paramref name="expectedHash"/>: snapshots are
    /// searched newest to oldest, the manifest hash pre-filters without re-hashing, and the
    /// actual copy is hash-verified again before it replaces the file.
    /// </summary>
    public bool Restore(FileInfo file, byte[] expectedHash, CancellationToken token = default) {
      if (file == null) throw new ArgumentNullException(nameof(file));
      if (expectedHash == null) throw new ArgumentNullException(nameof(expectedHash));

      var relativePath = file.RelativeTo(this.Root);

      foreach (var snapshot in this.EnumerateSnapshots()) {
        token.ThrowIfCancellationRequested();

        var manifest = SnapshotManifest.Load(snapshot);
        if (!manifest.TryGet(relativePath, out var entry))
          continue;

        byte[] entryHash;
        try {
          entryHash = entry.Hash;
        } catch (FormatException) {
          continue;
        }

        if (!entryHash.SequenceEqual(expectedHash))
          continue;

        var candidate = new FileInfo(Path.Combine(snapshot.FullName, relativePath));
        candidate.Refresh();
        if (!candidate.Exists)
          continue;

        try {

          // double-check: the backup copy itself may have rotted since it was taken
          if (!candidate.ComputeSHA512Hash().SequenceEqual(expectedHash))
            continue;

          var temporary = new FileInfo(file.FullName + ".fst-restore");
          try {
            candidate.CopyTo(temporary.FullName, true);

            file.Refresh();
            if (file.Exists) {
              file.Attributes &= ~FileAttributes.ReadOnly;
              file.Delete();
            }

            File.Move(temporary.FullName, file.FullName);
            return true;
          } finally {
            temporary.Refresh();
            if (temporary.Exists)
              temporary.Delete();
          }
        } catch (IOException) {
          ;
        } catch (UnauthorizedAccessException) {
          ;
        }
      }

      return false;
    }

    /// <summary>Complete snapshots, newest first.</summary>
    public IEnumerable<DirectoryInfo> EnumerateSnapshots() {
      this.BackupRoot.Refresh();
      if (!this.BackupRoot.Exists)
        return Enumerable.Empty<DirectoryInfo>();

      return this.BackupRoot
        .EnumerateDirectories()
        .Where(d => !d.Name.EndsWith(_PARTIAL_SUFFIX, StringComparison.OrdinalIgnoreCase) && _TryParseSnapshotTime(d.Name, out _))
        .OrderByDescending(d => d.Name, StringComparer.Ordinal)
        .ToArray();
    }

    public DirectoryInfo LatestSnapshot() => this.EnumerateSnapshots().FirstOrDefault();

    private int _Prune() {
      var snapshots = this.EnumerateSnapshots()
        .Select(d => (Directory: d, Time: _ParseSnapshotTime(d.Name)))
        .ToArray();

      var survivors = this._retention.SelectSurvivors(snapshots.Select(s => s.Time));

      var pruned = 0;
      foreach (var snapshot in snapshots) {
        if (survivors.Contains(snapshot.Time))
          continue;

        try {
          _DeleteRecursive(snapshot.Directory);
          ++pruned;
        } catch (IOException) {
          ;
        } catch (UnauthorizedAccessException) {
          ;
        }
      }

      return pruned;
    }

    private void _ReclaimLeftovers() {
      foreach (var leftover in this.BackupRoot.EnumerateDirectories("*" + _PARTIAL_SUFFIX)) {
        try {
          _DeleteRecursive(leftover);
        } catch (IOException) {
          ;
        } catch (UnauthorizedAccessException) {
          ;
        }
      }
    }

    private static void _DeleteRecursive(DirectoryInfo directory) {
      foreach (var file in directory.EnumerateFiles("*", SearchOption.AllDirectories))
        file.Attributes = FileAttributes.Normal;

      directory.Delete(true);
    }

    private string _UniqueSnapshotName(DateTime utc) {
      var baseName = utc.ToString(_SNAPSHOT_NAME_FORMAT, CultureInfo.InvariantCulture);
      var name = baseName;
      for (var counter = 2; ; ++counter) {
        if (!Directory.Exists(Path.Combine(this.BackupRoot.FullName, name))
            && !Directory.Exists(Path.Combine(this.BackupRoot.FullName, name + _PARTIAL_SUFFIX)))
          return name;

        name = $"{baseName}_{counter}";
      }
    }

    private static bool _TryParseSnapshotTime(string name, out DateTime time) {

      // tolerate the _N same-second suffix
      var core = name;
      var underscore = name.LastIndexOf('_');
      if (underscore > _SNAPSHOT_NAME_FORMAT.Length - 4 && int.TryParse(name.Substring(underscore + 1), out _))
        core = name.Substring(0, underscore);

      return DateTime.TryParseExact(core, _SNAPSHOT_NAME_FORMAT, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out time);
    }

    private static DateTime _ParseSnapshotTime(string name) {
      _TryParseSnapshotTime(name, out var result);
      return result;
    }

    private void _WarnLinkFallback(BackupReport report) {
      if (report.FellBackToCopy)
        return;

      report.FellBackToCopy = true;
      this._log?.Invoke($"Backup target {this.BackupRoot.FullName} does not support hard links - unchanged files are copied in full");
    }

  }
}
