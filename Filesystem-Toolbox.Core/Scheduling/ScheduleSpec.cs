using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Filesystem_Toolbox.Core.Scheduling {

  public enum ScheduleKind {
    /// <summary>Run every fixed duration ("every 90m", "every 6h", "every 2d").</summary>
    Interval,

    /// <summary>Run once per day at a wall-clock time ("daily 03:30").</summary>
    Daily,

    /// <summary>Run once per week on a given day at a wall-clock time ("weekly Sunday 03:30").</summary>
    Weekly,
  }

  /// <summary>
  /// A schedule with a compact human-editable string form. Due-ness is computed by
  /// <see cref="NextDue"/>: an action is due when <c>NextDue(lastRun, now) &lt;= now</c>.
  /// Interval schedules use pure duration math (immune to DST); daily/weekly anchor to
  /// local wall-clock time on purpose, so "daily 03:30" stays 03:30 across DST changes.
  /// A missed window (application was off) collapses into exactly one catch-up run.
  /// </summary>
  [JsonConverter(typeof(ScheduleSpecJsonConverter))]
  public readonly struct ScheduleSpec : IEquatable<ScheduleSpec> {

    public ScheduleKind Kind { get; }
    public TimeSpan Interval { get; }
    public TimeSpan TimeOfDay { get; }
    public DayOfWeek DayOfWeek { get; }

    private ScheduleSpec(ScheduleKind kind, TimeSpan interval, TimeSpan timeOfDay, DayOfWeek dayOfWeek) {
      this.Kind = kind;
      this.Interval = interval;
      this.TimeOfDay = timeOfDay;
      this.DayOfWeek = dayOfWeek;
    }

    public static ScheduleSpec Every(TimeSpan interval) {
      if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));

      return new ScheduleSpec(ScheduleKind.Interval, interval, TimeSpan.Zero, default);
    }

    public static ScheduleSpec DailyAt(TimeSpan timeOfDay) {
      if (timeOfDay < TimeSpan.Zero || timeOfDay >= TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(timeOfDay));

      return new ScheduleSpec(ScheduleKind.Daily, TimeSpan.Zero, timeOfDay, default);
    }

    public static ScheduleSpec WeeklyAt(DayOfWeek day, TimeSpan timeOfDay) {
      if (timeOfDay < TimeSpan.Zero || timeOfDay >= TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(timeOfDay));

      return new ScheduleSpec(ScheduleKind.Weekly, TimeSpan.Zero, timeOfDay, day);
    }

    /// <exception cref="FormatException">when the text is not a valid schedule</exception>
    public static ScheduleSpec Parse(string text) {
      if (!TryParse(text, out var result))
        throw new FormatException($"Invalid schedule: '{text}'");

      return result;
    }

    public static bool TryParse(string text, out ScheduleSpec result) {
      result = default;
      if (string.IsNullOrWhiteSpace(text))
        return false;

      var parts = text.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
      switch (parts[0].ToLowerInvariant()) {
        case "every" when parts.Length == 2:
          return _TryParseInterval(parts[1], out result);

        case "daily" when parts.Length == 2: {
          if (!_TryParseTimeOfDay(parts[1], out var timeOfDay))
            return false;

          result = DailyAt(timeOfDay);
          return true;
        }

        case "weekly" when parts.Length == 3: {
          if (!_TryParseDayOfWeek(parts[1], out var day))
            return false;

          if (!_TryParseTimeOfDay(parts[2], out var timeOfDay))
            return false;

          result = WeeklyAt(day, timeOfDay);
          return true;
        }

        default:
          return false;
      }
    }

    private static bool _TryParseInterval(string token, out ScheduleSpec result) {
      result = default;
      if (token.Length < 2)
        return false;

      var unit = char.ToLowerInvariant(token[token.Length - 1]);
      if (!int.TryParse(token.Substring(0, token.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var amount) || amount < 1)
        return false;

      TimeSpan interval;
      switch (unit) {
        case 'm': interval = TimeSpan.FromMinutes(amount); break;
        case 'h': interval = TimeSpan.FromHours(amount); break;
        case 'd': interval = TimeSpan.FromDays(amount); break;
        default: return false;
      }

      result = Every(interval);
      return true;
    }

    private static bool _TryParseTimeOfDay(string token, out TimeSpan result) {
      result = default;

      // strictly HH:mm, two digits each
      if (token.Length != 5 || token[2] != ':')
        return false;

      if (!int.TryParse(token.Substring(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
          || !int.TryParse(token.Substring(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
        return false;

      if (hours > 23 || minutes > 59)
        return false;

      result = new TimeSpan(hours, minutes, 0);
      return true;
    }

    private static bool _TryParseDayOfWeek(string token, out DayOfWeek result) {
      foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek))) {
        var name = day.ToString();
        if (string.Equals(token, name, StringComparison.OrdinalIgnoreCase)
            || (token.Length == 3 && string.Equals(token, name.Substring(0, 3), StringComparison.OrdinalIgnoreCase))) {
          result = day;
          return true;
        }
      }

      result = default;
      return false;
    }

    public override string ToString() {
      switch (this.Kind) {
        case ScheduleKind.Interval: {
          var total = this.Interval;
          if (total.TotalDays >= 1 && total.TotalDays == Math.Floor(total.TotalDays))
            return $"every {(int)total.TotalDays}d";

          if (total.TotalHours >= 1 && total.TotalHours == Math.Floor(total.TotalHours))
            return $"every {(int)total.TotalHours}h";

          return $"every {(int)total.TotalMinutes}m";
        }

        case ScheduleKind.Daily:
          return $"daily {this.TimeOfDay.Hours:00}:{this.TimeOfDay.Minutes:00}";

        case ScheduleKind.Weekly:
          return $"weekly {this.DayOfWeek} {this.TimeOfDay.Hours:00}:{this.TimeOfDay.Minutes:00}";

        default:
          return string.Empty;
      }
    }

    /// <summary>
    /// The first instant at or after which the action is (or becomes) due, given the last
    /// successful run. The due test is <c>NextDue(lastRun, now) &lt;= now</c>.
    /// </summary>
    public DateTime NextDue(DateTime? lastRun, DateTime now) {
      switch (this.Kind) {
        case ScheduleKind.Interval:
          return lastRun == null ? now : lastRun.Value + this.Interval;

        case ScheduleKind.Daily:
          return this._NextBoundaryDue(lastRun, now, TimeSpan.FromDays(1), this._MostRecentDailyBoundary(now));

        case ScheduleKind.Weekly:
          return this._NextBoundaryDue(lastRun, now, TimeSpan.FromDays(7), this._MostRecentWeeklyBoundary(now));

        default:
          throw new InvalidOperationException();
      }
    }

    /// <summary>
    /// Boundary rule shared by daily/weekly: if the last run predates the most recent boundary
    /// (or never happened), that boundary is due now; otherwise the next boundary is in the future.
    /// </summary>
    private DateTime _NextBoundaryDue(DateTime? lastRun, DateTime now, TimeSpan period, DateTime lastBoundary)
      => lastRun == null || lastRun.Value < lastBoundary ? lastBoundary : lastBoundary + period;

    private DateTime _MostRecentDailyBoundary(DateTime now) {
      var candidate = now.Date + this.TimeOfDay;
      return candidate <= now ? candidate : candidate - TimeSpan.FromDays(1);
    }

    private DateTime _MostRecentWeeklyBoundary(DateTime now) {
      var daysBack = ((int)now.DayOfWeek - (int)this.DayOfWeek + 7) % 7;
      var candidate = now.Date.AddDays(-daysBack) + this.TimeOfDay;
      return candidate <= now ? candidate : candidate - TimeSpan.FromDays(7);
    }

    public bool Equals(ScheduleSpec other)
      => this.Kind == other.Kind
      && this.Interval == other.Interval
      && this.TimeOfDay == other.TimeOfDay
      && this.DayOfWeek == other.DayOfWeek
      ;

    public override bool Equals(object obj) => obj is ScheduleSpec other && this.Equals(other);

    public override int GetHashCode() => (int)this.Kind ^ this.Interval.GetHashCode() ^ this.TimeOfDay.GetHashCode() << 4 ^ (int)this.DayOfWeek << 28;

    public static bool operator ==(ScheduleSpec left, ScheduleSpec right) => left.Equals(right);
    public static bool operator !=(ScheduleSpec left, ScheduleSpec right) => !left.Equals(right);

  }

  /// <summary>
  /// Serializes as the canonical compact string; accepts the string form or a single-key
  /// object form like <c>{"every":"90m"}</c>, <c>{"daily":"03:30"}</c>, <c>{"weekly":"Sun 03:30"}</c>.
  /// </summary>
  public sealed class ScheduleSpecJsonConverter : JsonConverter<ScheduleSpec> {

    public override ScheduleSpec Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
      switch (reader.TokenType) {
        case JsonTokenType.String: {
          var text = reader.GetString();
          if (!ScheduleSpec.TryParse(text, out var result))
            throw new JsonException($"Invalid schedule: '{text}'");

          return result;
        }

        case JsonTokenType.StartObject: {
          if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName)
            throw new JsonException("Expected a single schedule property");

          var key = reader.GetString();
          if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            throw new JsonException("Expected a schedule value string");

          var value = reader.GetString();
          if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
            throw new JsonException("Expected a single-key schedule object");

          if (!ScheduleSpec.TryParse($"{key} {value}", out var result))
            throw new JsonException($"Invalid schedule: '{key} {value}'");

          return result;
        }

        default:
          throw new JsonException($"Unexpected token {reader.TokenType} for a schedule");
      }
    }

    public override void Write(Utf8JsonWriter writer, ScheduleSpec value, JsonSerializerOptions options)
      => writer.WriteStringValue(value.ToString());

  }
}
