using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Filesystem_Toolbox.Core.Scheduling;

namespace Filesystem_Toolbox.Core.Configuration {

  /// <summary>
  /// Pure, immutable settings resolution: for any path, walk the configured entries from the
  /// deepest ancestor upward taking the first non-null value per setting, then the global
  /// configuration, then <see cref="ConfigurationDefaults"/>. Built once per applied
  /// configuration - resolution is allocation-light and thread-safe.
  /// </summary>
  public sealed class ConfigurationResolver {

    private sealed class Entry {
      public string NormalizedPath;
      public WatchedFolderConfiguration Configuration;
    }

    private readonly List<Entry> _entries;
    private readonly ScheduleSpec? _globalVerifySchedule;

    public ConfigurationResolver(IEnumerable<WatchedFolderConfiguration> folders, ScheduleSpec? globalVerifySchedule = null) {
      if (folders == null) throw new ArgumentNullException(nameof(folders));

      this._globalVerifySchedule = globalVerifySchedule;
      this._entries = new List<Entry>();
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var folder in folders) {
        if (folder?.Path == null || folder.Path.IsNullOrWhiteSpace())
          continue;

        var normalized = _Normalize(folder.Path);
        if (!seen.Add(normalized))
          continue; // exact duplicates: first one wins

        this._entries.Add(new Entry { NormalizedPath = normalized, Configuration = folder });
      }
    }

    public static ConfigurationResolver For(ToolboxConfiguration configuration)
      => new ConfigurationResolver(configuration?.Folders ?? Enumerable.Empty<WatchedFolderConfiguration>(), configuration?.VerifySchedule);

    /// <summary>Entries not nested inside another configured path - these are the watch roots.</summary>
    public IReadOnlyList<WatchedFolderConfiguration> WatchRoots
      => this._entries
        .Where(e => !this._entries.Any(other => !ReferenceEquals(other, e) && _IsStrictlyUnder(e.NormalizedPath, other.NormalizedPath)))
        .Select(e => e.Configuration)
        .ToArray()
      ;

    /// <summary>Whether the path lies under at least one configured entry.</summary>
    public bool IsCovered(string path) {
      var normalized = _Normalize(path);
      return this._entries.Any(e => _IsAtOrUnder(normalized, e.NormalizedPath));
    }

    public EffectiveSettings Resolve(FileSystemInfo path) => this.Resolve(path?.FullName);

    public EffectiveSettings Resolve(string path) {
      if (path == null) throw new ArgumentNullException(nameof(path));

      var normalized = _Normalize(path);

      // deepest configured ancestor first
      var chain = this._entries
        .Where(e => _IsAtOrUnder(normalized, e.NormalizedPath))
        .OrderByDescending(e => e.NormalizedPath.Length)
        .Select(e => e.Configuration)
        .ToArray();

      return new EffectiveSettings(
        _First(chain, f => f.ParityRedundancyPercent) ?? ConfigurationDefaults.PARITY_REDUNDANCY_PERCENT,
        _First(chain, f => f.AutoRepair) ?? ConfigurationDefaults.AUTO_REPAIR,
        _First(chain, f => f.RefreshIntervalDays) ?? ConfigurationDefaults.REFRESH_INTERVAL_DAYS,
        _FirstText(chain, f => f.OnCorruptionCommand),
        _First(chain, f => f.DedupEnabled) ?? ConfigurationDefaults.DEDUP_ENABLED,
        _First(chain, f => f.VerifySchedule) ?? this._globalVerifySchedule ?? ConfigurationDefaults.VERIFY_SCHEDULE,
        _FirstText(chain, f => f.BackupPath),
        _First(chain, f => f.BackupSchedule),
        _First(chain, f => f.GfsKeepDaily) ?? ConfigurationDefaults.GFS_KEEP_DAILY,
        _First(chain, f => f.GfsKeepWeekly) ?? ConfigurationDefaults.GFS_KEEP_WEEKLY,
        _First(chain, f => f.GfsKeepMonthly) ?? ConfigurationDefaults.GFS_KEEP_MONTHLY,
        _First(chain, f => f.DegradationWarningErrorsPerMonth) ?? ConfigurationDefaults.DEGRADATION_WARNING_ERRORS_PER_MONTH,
        _First(chain, f => f.ToastNotifications) ?? ConfigurationDefaults.TOAST_NOTIFICATIONS
      );
    }

    private static T? _First<T>(WatchedFolderConfiguration[] chain, Func<WatchedFolderConfiguration, T?> selector) where T : struct {
      foreach (var entry in chain) {
        var value = selector(entry);
        if (value != null)
          return value;
      }

      return null;
    }

    /// <summary>Strings inherit on null AND on blank - an emptied text box must not shadow an ancestor's value.</summary>
    private static string _FirstText(WatchedFolderConfiguration[] chain, Func<WatchedFolderConfiguration, string> selector) {
      foreach (var entry in chain) {
        var value = selector(entry);
        if (!value.IsNullOrWhiteSpace())
          return value;
      }

      return null;
    }

    private static string _Normalize(string path)
      => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool _IsAtOrUnder(string child, string ancestor)
      => child.Equals(ancestor, StringComparison.OrdinalIgnoreCase)
      || child.StartsWith(ancestor + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
      ;

    private static bool _IsStrictlyUnder(string child, string ancestor)
      => child.StartsWith(ancestor + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

  }
}
