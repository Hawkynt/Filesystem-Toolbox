using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Filesystem_Toolbox.Core.Integrity;

namespace Filesystem_Toolbox.Core.Services {

  public sealed class RefreshReport {

    public int Refreshed { get; internal set; }
    public int SkippedNotDue { get; internal set; }
    public int SkippedDirty { get; internal set; }
    public int Errors { get; internal set; }

  }

  /// <summary>
  /// Preventive care for flash media: NAND cells lose charge over years without power, so
  /// files are periodically rewritten (read - verify - write - flush to device) to recharge
  /// them. Every rewrite costs one program/erase cycle; the default 180-day interval means
  /// about two cycles per year, negligible against typical NAND endurance, while staying
  /// well inside consumer retention windows. Pointless on managed SSDs (they scrub
  /// themselves) - this targets passive USB sticks and SD cards.
  /// A file is only ever rewritten when its content verifies clean: rewriting a corrupted
  /// file would make the corruption permanent and defeat the parity store.
  /// </summary>
  public sealed class RefreshService {

    private const string _REFRESH_DATABASE_NAME = "refresh.db";

    private readonly FolderIntegrityChecker _checker;
    private readonly TimeSpan _interval;

    public RefreshService(FolderIntegrityChecker checker, TimeSpan interval) {
      if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));

      this._checker = checker ?? throw new ArgumentNullException(nameof(checker));
      this._interval = interval;
    }

    private FileInfo _RefreshDatabaseFile => new FileInfo(Path.Combine(
      this._checker.RootDirectory.FullName,
      FolderIntegrityChecker.PROTECTED_FOLDER_NAME,
      _REFRESH_DATABASE_NAME
    ));

    public DateTime? GetLastRefresh(FileInfo file) {
      var timestamps = this._LoadTimestamps();
      var key = file.RelativeTo(this._checker.RootDirectory);
      return timestamps.TryGetValue(key, out var ticks) ? new DateTime(ticks, DateTimeKind.Utc) : (DateTime?)null;
    }

    /// <summary>Rewrites every tracked, verified-clean file whose last refresh (or last write) is older than the interval.</summary>
    public RefreshReport RefreshDue(CancellationToken token = default) {
      var report = new RefreshReport();
      var timestamps = this._LoadTimestamps();
      var now = DateTime.UtcNow;

      foreach (var pair in this._checker.GetDatabaseSnapshot()) {
        token.ThrowIfCancellationRequested();

        if (!ChecksumEntry.TryParse(pair.Value, out var stored))
          continue;

        var file = this._checker.GetFile(pair.Key);
        file.Refresh();
        if (!file.Exists) {
          ++report.Errors;
          continue;
        }

        // age = time since whichever happened last: explicit refresh or a real write
        var lastCare = file.LastWriteTimeUtc;
        if (timestamps.TryGetValue(pair.Key, out var ticks)) {
          var lastRefresh = new DateTime(ticks, DateTimeKind.Utc);
          if (lastRefresh > lastCare)
            lastCare = lastRefresh;
        }

        if (now - lastCare < this._interval) {
          ++report.SkippedNotDue;
          continue;
        }

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

        try {
          _RewriteInPlace(file);
          timestamps[pair.Key] = now.Ticks;
          ++report.Refreshed;
        } catch (IOException) {
          ++report.Errors;
        } catch (UnauthorizedAccessException) {
          ++report.Errors;
        }
      }

      this._SaveTimestamps(timestamps);
      return report;
    }

    /// <summary>
    /// Read - rewrite - flush-to-device - atomic replace, preserving timestamps and attributes
    /// so the refresh is invisible to integrity classification.
    /// </summary>
    private static void _RewriteInPlace(FileInfo file) {
      var originalLastWrite = file.LastWriteTimeUtc;
      var originalCreation = file.CreationTimeUtc;
      var originalAttributes = file.Attributes;

      var temporary = new FileInfo(file.FullName + ".fst-refresh");
      try {
        using (var source = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan))
        using (var target = new FileStream(temporary.FullName, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16)) {
          source.CopyTo(target);
          target.Flush(true); // force the device to actually program the cells
        }

        file.Attributes = FileAttributes.Normal;
        file.Delete();
        File.Move(temporary.FullName, file.FullName);

        File.SetCreationTimeUtc(file.FullName, originalCreation);
        File.SetLastWriteTimeUtc(file.FullName, originalLastWrite);
        if (originalAttributes != FileAttributes.Normal)
          File.SetAttributes(file.FullName, originalAttributes);
      } finally {
        temporary.Refresh();
        if (temporary.Exists)
          temporary.Delete();
      }
    }

    private Dictionary<string, long> _LoadTimestamps() {
      var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
      var file = this._RefreshDatabaseFile;
      file.Refresh();
      if (!file.Exists)
        return result;

      foreach (var line in File.ReadAllLines(file.FullName)) {
        var index = line.IndexOf("=>", StringComparison.Ordinal);
        if (index < 1)
          continue;

        if (long.TryParse(line.Substring(0, index).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
          result[line.Substring(index + 2).Trim()] = ticks;
      }

      return result;
    }

    private void _SaveTimestamps(Dictionary<string, long> timestamps) {
      var file = this._RefreshDatabaseFile;
      file.Directory.Create();
      File.WriteAllLines(file.FullName, timestamps.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase).Select(p => $"{p.Value} => {p.Key}"));
    }

  }
}
