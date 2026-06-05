using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Filesystem_Toolbox.Core.Statistics {

  public enum DegradationStatus {

    /// <summary>Error rate below the configured threshold.</summary>
    Healthy,

    /// <summary>Errors this month reached the warning threshold - the medium is degrading.</summary>
    Degrading,

    /// <summary>Errors at three times the threshold, or unrepairable damage within 30 days.</summary>
    Failing,

  }

  /// <summary>KPIs of one watched root, computed from the event history.</summary>
  public sealed class RootStatistics {

    public string Root { get; internal set; }
    public int ErrorsFoundTotal { get; internal set; }
    public int ErrorsFound30d { get; internal set; }
    public int ErrorsFound7d { get; internal set; }
    public int ErrorsCorrectedTotal { get; internal set; }
    public int ErrorsCorrected30d { get; internal set; }
    public int ErrorsCorrected7d { get; internal set; }

    /// <summary>(year, month, found, corrected) for the last twelve calendar months, oldest first.</summary>
    public IReadOnlyList<(int Year, int Month, int Found, int Corrected)> ByMonthLast12 { get; internal set; }

    /// <summary>Mean time between bit-rot findings; null with fewer than two findings.</summary>
    public TimeSpan? MeanTimeBetweenFailures { get; internal set; }

    public string MeanTimeBetweenFailuresHuman => this.MeanTimeBetweenFailures == null
      ? "no failures yet"
      : this.MeanTimeBetweenFailures.Value.TotalDays < 90
        ? $"{this.MeanTimeBetweenFailures.Value.TotalDays:0.#} days"
        : $"{this.MeanTimeBetweenFailures.Value.TotalDays / 30.44:0.#} months"
      ;

    public DegradationStatus Degradation { get; internal set; }

    /// <summary>Per-status problem counts of the most recent verify run (for the status pie).</summary>
    public IReadOnlyDictionary<string, int> LastVerifyDistribution { get; internal set; }

  }

  /// <summary>
  /// Computes per-root KPIs - errors found/corrected over several windows, MTBF, monthly
  /// history, degradation status - from the append-only event log.
  /// </summary>
  public sealed class StatisticsService {

    private static readonly EventType[] _FOUND_TYPES = { EventType.BitRotFound, EventType.Unrepairable };
    private static readonly EventType[] _CORRECTED_TYPES = { EventType.Repaired, EventType.RepairedFromBackup };

    private readonly EventLog _log;
    private readonly Func<DateTime> _utcNow;

    public StatisticsService(EventLog log, Func<DateTime> utcNow = null) {
      this._log = log ?? throw new ArgumentNullException(nameof(log));
      this._utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>All roots that ever produced an event.</summary>
    public IReadOnlyList<string> KnownRoots()
      => this._log.ReadAll().Select(e => e.Root).Where(r => r != null).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public RootStatistics For(string root, int degradationThresholdPerMonth) {
      if (root == null) throw new ArgumentNullException(nameof(root));
      if (degradationThresholdPerMonth < 1) throw new ArgumentOutOfRangeException(nameof(degradationThresholdPerMonth));

      var now = this._utcNow();
      var events = this._log.ReadAll().Where(e => string.Equals(e.Root, root, StringComparison.OrdinalIgnoreCase)).ToArray();

      var found = events.Where(e => _FOUND_TYPES.Contains(e.Type)).ToArray();
      var corrected = events.Where(e => _CORRECTED_TYPES.Contains(e.Type)).ToArray();

      var result = new RootStatistics {
        Root = root,
        ErrorsFoundTotal = found.Length,
        ErrorsFound30d = found.Count(e => e.Utc >= now.AddDays(-30)),
        ErrorsFound7d = found.Count(e => e.Utc >= now.AddDays(-7)),
        ErrorsCorrectedTotal = corrected.Length,
        ErrorsCorrected30d = corrected.Count(e => e.Utc >= now.AddDays(-30)),
        ErrorsCorrected7d = corrected.Count(e => e.Utc >= now.AddDays(-7)),
        ByMonthLast12 = _ByMonth(found, corrected, now),
        MeanTimeBetweenFailures = _Mtbf(found),
        LastVerifyDistribution = _LastVerifyDistribution(events),
      };

      result.Degradation = this._ClassifyDegradation(found, now, degradationThresholdPerMonth);
      return result;
    }

    /// <summary>
    /// Whether this root just crossed its monthly error threshold and no warning was issued
    /// today - the caller then appends the <see cref="EventType.DeviceWarning"/> and notifies.
    /// </summary>
    public bool CrossedThresholdToday(string root, int thresholdPerMonth) {
      var now = this._utcNow();
      var events = this._log.ReadAll().Where(e => string.Equals(e.Root, root, StringComparison.OrdinalIgnoreCase)).ToArray();

      var errorsThisMonth = events.Count(e => _FOUND_TYPES.Contains(e.Type) && e.Utc.Year == now.Year && e.Utc.Month == now.Month);
      if (errorsThisMonth < thresholdPerMonth)
        return false;

      // the DeviceWarning event itself is the once-per-day marker - no extra state file
      return !events.Any(e => e.Type == EventType.DeviceWarning && e.Utc.Date == now.Date);
    }

    private DegradationStatus _ClassifyDegradation(EventRecord[] found, DateTime now, int threshold) {
      var unrepairableRecently = found.Any(e => e.Type == EventType.Unrepairable && e.Utc >= now.AddDays(-30));
      var errorsThisMonth = found.Count(e => e.Utc.Year == now.Year && e.Utc.Month == now.Month);

      if (unrepairableRecently || errorsThisMonth >= 3 * threshold)
        return DegradationStatus.Failing;

      return errorsThisMonth >= threshold ? DegradationStatus.Degrading : DegradationStatus.Healthy;
    }

    private static TimeSpan? _Mtbf(EventRecord[] found) {
      var rotTimes = found.Where(e => e.Type == EventType.BitRotFound).Select(e => e.Utc).OrderBy(t => t).ToArray();
      if (rotTimes.Length < 2)
        return null;

      return TimeSpan.FromTicks((rotTimes[rotTimes.Length - 1] - rotTimes[0]).Ticks / (rotTimes.Length - 1));
    }

    private static IReadOnlyList<(int, int, int, int)> _ByMonth(EventRecord[] found, EventRecord[] corrected, DateTime now) {
      var result = new List<(int, int, int, int)>();
      for (var offset = 11; offset >= 0; --offset) {
        var month = new DateTime(now.Year, now.Month, 1).AddMonths(-offset);
        result.Add((
          month.Year,
          month.Month,
          found.Count(e => e.Utc.Year == month.Year && e.Utc.Month == month.Month),
          corrected.Count(e => e.Utc.Year == month.Year && e.Utc.Month == month.Month)
        ));
      }

      return result;
    }

    private static IReadOnlyDictionary<string, int> _LastVerifyDistribution(EventRecord[] events) {
      var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      var lastRun = events.Where(e => e.Type == EventType.VerifyRun).OrderBy(e => e.Utc).LastOrDefault();
      if (lastRun == null)
        return result;

      var problems = 0;
      if (!string.IsNullOrWhiteSpace(lastRun.Detail))
        foreach (var pair in lastRun.Detail.Split(';')) {
          var parts = pair.Split('=');
          if (parts.Length == 2 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)) {
            result[parts[0]] = count;
            problems += count;
          }
        }

      if (lastRun.FilesChecked != null)
        result["Ok"] = Math.Max(0, lastRun.FilesChecked.Value - problems);

      return result;
    }

  }
}
