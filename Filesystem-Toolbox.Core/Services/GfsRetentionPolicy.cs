using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Filesystem_Toolbox.Core.Services {

  /// <summary>
  /// Grandfather-father-son retention: keep the newest snapshot of each of the last N days,
  /// M ISO weeks and K months; everything else may be pruned. A snapshot satisfying several
  /// buckets is kept once; the newest snapshot overall is always kept.
  /// </summary>
  public readonly struct GfsRetentionPolicy {

    public int KeepDaily { get; }
    public int KeepWeekly { get; }
    public int KeepMonthly { get; }

    public GfsRetentionPolicy(int keepDaily, int keepWeekly, int keepMonthly) {
      if (keepDaily < 0) throw new ArgumentOutOfRangeException(nameof(keepDaily));
      if (keepWeekly < 0) throw new ArgumentOutOfRangeException(nameof(keepWeekly));
      if (keepMonthly < 0) throw new ArgumentOutOfRangeException(nameof(keepMonthly));

      this.KeepDaily = keepDaily;
      this.KeepWeekly = keepWeekly;
      this.KeepMonthly = keepMonthly;
    }

    public static GfsRetentionPolicy Default => new GfsRetentionPolicy(
      Configuration.ConfigurationDefaults.GFS_KEEP_DAILY,
      Configuration.ConfigurationDefaults.GFS_KEEP_WEEKLY,
      Configuration.ConfigurationDefaults.GFS_KEEP_MONTHLY
    );

    /// <summary>
    /// Selects which timestamps survive. Pure - the caller maps the result back to snapshots.
    /// </summary>
    public IReadOnlyCollection<DateTime> SelectSurvivors(IEnumerable<DateTime> snapshotTimes) {
      if (snapshotTimes == null) throw new ArgumentNullException(nameof(snapshotTimes));

      var ordered = snapshotTimes.OrderByDescending(t => t).ToArray();
      var keep = new HashSet<DateTime>();
      if (ordered.Length == 0)
        return keep;

      // the two newest snapshots always survive, whatever the policy says: the newest is the
      // current state, and its predecessor is both the dedup link base and the immediate
      // fallback should the freshest snapshot itself turn out damaged
      keep.Add(ordered[0]);
      if (ordered.Length > 1)
        keep.Add(ordered[1]);

      _KeepNewestPerBucket(ordered, keep, this.KeepDaily, t => t.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
      _KeepNewestPerBucket(ordered, keep, this.KeepWeekly, t => $"{_IsoWeekYear(t)}-W{_IsoWeek(t):00}");
      _KeepNewestPerBucket(ordered, keep, this.KeepMonthly, t => t.ToString("yyyy-MM", CultureInfo.InvariantCulture));

      return keep;
    }

    private static void _KeepNewestPerBucket(DateTime[] orderedDescending, HashSet<DateTime> keep, int bucketCount, Func<DateTime, string> bucketKey) {
      var seenBuckets = new HashSet<string>(StringComparer.Ordinal);
      foreach (var timestamp in orderedDescending) {
        if (seenBuckets.Count >= bucketCount && !seenBuckets.Contains(bucketKey(timestamp)))
          break;

        // the first (newest) snapshot of each bucket is its representative
        if (seenBuckets.Add(bucketKey(timestamp)))
          keep.Add(timestamp);
      }
    }

    // ISO-8601 week math, portable across net48 and net8 (System.Globalization.ISOWeek is net Core 3+ only)
    private static int _IsoWeek(DateTime time) {
      var day = (int)CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(time);
      if (day == 0)
        day = 7;

      var thursday = time.AddDays(4 - day);
      return (thursday.DayOfYear - 1) / 7 + 1;
    }

    private static int _IsoWeekYear(DateTime time) {
      var day = (int)CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(time);
      if (day == 0)
        day = 7;

      return time.AddDays(4 - day).Year;
    }

  }
}
