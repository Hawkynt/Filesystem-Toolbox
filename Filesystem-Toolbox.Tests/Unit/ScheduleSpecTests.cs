using System.Text.Json;
using Filesystem_Toolbox.Core.Scheduling;

namespace Filesystem_Toolbox.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class ScheduleSpecTests {

  #region parsing and canonical form

  [TestCase("every 90m", "every 90m")]
  [TestCase("every 60m", "every 1h", Description = "whole hours canonicalize to h")]
  [TestCase("every 6h", "every 6h")]
  [TestCase("every 24h", "every 1d", Description = "whole days canonicalize to d")]
  [TestCase("every 2d", "every 2d")]
  [TestCase("EVERY 5M", "every 5m", Description = "case-insensitive")]
  [TestCase("daily 03:30", "daily 03:30")]
  [TestCase("daily 00:00", "daily 00:00")]
  [TestCase("daily 23:59", "daily 23:59")]
  [TestCase("weekly Sunday 03:30", "weekly Sunday 03:30")]
  [TestCase("weekly Sun 03:30", "weekly Sunday 03:30", Description = "3-letter day accepted")]
  [TestCase("weekly mon 00:00", "weekly Monday 00:00")]
  public void Given_ValidText_When_Parsing_Then_CanonicalRoundTrip(string input, string canonical) {
    var spec = ScheduleSpec.Parse(input);

    Assert.Multiple(() => {
      Assert.That(spec.ToString(), Is.EqualTo(canonical));
      Assert.That(ScheduleSpec.Parse(spec.ToString()), Is.EqualTo(spec), "canonical form must reparse identically");
    });
  }

  [TestCase(null)]
  [TestCase("")]
  [TestCase("   ")]
  [TestCase("every")]
  [TestCase("every 0m")]
  [TestCase("every -5m")]
  [TestCase("every 90x")]
  [TestCase("every m")]
  [TestCase("daily 24:00")]
  [TestCase("daily 3:30", Description = "single-digit hour rejected for strictness")]
  [TestCase("daily 03:60")]
  [TestCase("daily 0330")]
  [TestCase("weekly Funday 03:30")]
  [TestCase("weekly Sunday")]
  [TestCase("nonsense")]
  public void Given_InvalidText_When_Parsing_Then_Rejected(string? input) {
    Assert.Multiple(() => {
      Assert.That(ScheduleSpec.TryParse(input!, out _), Is.False);
      Assert.That(() => ScheduleSpec.Parse(input!), Throws.InstanceOf<FormatException>());
    });
  }

  [Test]
  public void Given_InvalidFactoryArguments_When_Constructing_Then_ArgumentOutOfRangeExceptionIsThrown() {
    Assert.Multiple(() => {
      Assert.That(() => ScheduleSpec.Every(TimeSpan.Zero), Throws.InstanceOf<ArgumentOutOfRangeException>());
      Assert.That(() => ScheduleSpec.DailyAt(TimeSpan.FromDays(1)), Throws.InstanceOf<ArgumentOutOfRangeException>());
      Assert.That(() => ScheduleSpec.WeeklyAt(DayOfWeek.Monday, TimeSpan.FromMinutes(-1)), Throws.InstanceOf<ArgumentOutOfRangeException>());
    });
  }

  #endregion

  #region JSON round trip

  [Test]
  public void Given_Spec_When_SerializedToJson_Then_CompactStringIsEmitted() {
    var json = JsonSerializer.Serialize(ScheduleSpec.DailyAt(new TimeSpan(3, 30, 0)));

    Assert.That(json, Is.EqualTo("\"daily 03:30\""));
  }

  [TestCase("\"every 90m\"")]
  [TestCase("{\"every\":\"90m\"}", Description = "object form is accepted on input")]
  public void Given_JsonForms_When_Deserializing_Then_SameSpecResults(string json) {
    var spec = JsonSerializer.Deserialize<ScheduleSpec>(json);

    Assert.That(spec, Is.EqualTo(ScheduleSpec.Every(TimeSpan.FromMinutes(90))));
  }

  [Test]
  public void Given_NullableSpec_When_RoundTripping_Then_NullAndValueBothSurvive() {
    var holder = new { A = (ScheduleSpec?)ScheduleSpec.Parse("weekly Sun 02:00"), B = (ScheduleSpec?)null };

    var json = JsonSerializer.Serialize(holder);
    using var parsed = JsonDocument.Parse(json);

    Assert.Multiple(() => {
      Assert.That(parsed.RootElement.GetProperty("A").GetString(), Is.EqualTo("weekly Sunday 02:00"));
      Assert.That(parsed.RootElement.GetProperty("B").ValueKind, Is.EqualTo(JsonValueKind.Null));
    });
  }

  [Test]
  public void Given_InvalidJsonSchedule_When_Deserializing_Then_JsonExceptionIsThrown()
    => Assert.That(() => JsonSerializer.Deserialize<ScheduleSpec>("\"every never\""), Throws.InstanceOf<JsonException>());

  #endregion

  #region NextDue - interval

  private static readonly DateTime _NOW = new(2026, 6, 4, 12, 0, 0, DateTimeKind.Local);

  [Test]
  public void Given_IntervalNeverRun_When_ComputingNextDue_Then_DueImmediately() {
    var spec = ScheduleSpec.Every(TimeSpan.FromMinutes(10));

    Assert.That(spec.NextDue(null, _NOW), Is.LessThanOrEqualTo(_NOW));
  }

  [Test]
  public void Given_IntervalLastRunExactlyOneIntervalAgo_When_ComputingNextDue_Then_DueExactlyNow() {
    var spec = ScheduleSpec.Every(TimeSpan.FromMinutes(10));

    Assert.That(spec.NextDue(_NOW - TimeSpan.FromMinutes(10), _NOW), Is.EqualTo(_NOW));
  }

  [Test]
  public void Given_IntervalMissedWhileOff_When_ComputingNextDue_Then_DueInThePast() {
    var spec = ScheduleSpec.Every(TimeSpan.FromMinutes(10));

    Assert.That(spec.NextDue(_NOW - TimeSpan.FromDays(3), _NOW), Is.LessThan(_NOW), "one catch-up run is due");
  }

  [Test]
  public void Given_IntervalJustRun_When_ComputingNextDue_Then_NotDueYet() {
    var spec = ScheduleSpec.Every(TimeSpan.FromMinutes(10));

    Assert.That(spec.NextDue(_NOW, _NOW), Is.GreaterThan(_NOW));
  }

  #endregion

  #region NextDue - daily

  [Test]
  public void Given_DailyBeforeTodaysBoundary_When_NeverRun_Then_YesterdaysBoundaryIsDue() {
    var spec = ScheduleSpec.Parse("daily 13:00");
    var now = new DateTime(2026, 6, 4, 12, 0, 0); // before today's 13:00

    var due = spec.NextDue(null, now);

    Assert.That(due, Is.EqualTo(new DateTime(2026, 6, 3, 13, 0, 0)), "never ran -> the most recent past boundary is due");
  }

  [Test]
  public void Given_DailyRanYesterday_When_PastTodaysBoundary_Then_TodaysBoundaryIsDue() {
    var spec = ScheduleSpec.Parse("daily 03:30");
    var now = new DateTime(2026, 6, 4, 4, 0, 0);
    var lastRun = new DateTime(2026, 6, 3, 3, 30, 0);

    Assert.That(spec.NextDue(lastRun, now), Is.EqualTo(new DateTime(2026, 6, 4, 3, 30, 0)));
  }

  [Test]
  public void Given_DailyAlreadyRanToday_When_ComputingNextDue_Then_TomorrowsBoundary() {
    var spec = ScheduleSpec.Parse("daily 03:30");
    var now = new DateTime(2026, 6, 4, 4, 0, 0);
    var lastRun = new DateTime(2026, 6, 4, 3, 31, 0);

    Assert.That(spec.NextDue(lastRun, now), Is.EqualTo(new DateTime(2026, 6, 5, 3, 30, 0)));
  }

  [Test]
  public void Given_DailyCrossingMidnight_When_JustAfterBoundary_Then_Due() {
    var spec = ScheduleSpec.Parse("daily 00:30");
    var now = new DateTime(2026, 6, 4, 0, 31, 0);
    var lastRun = new DateTime(2026, 6, 3, 23, 0, 0);

    Assert.That(spec.NextDue(lastRun, now), Is.LessThanOrEqualTo(now));
  }

  [Test]
  public void Given_DailyCrossingMidnight_When_JustBeforeBoundary_Then_NotDue() {
    var spec = ScheduleSpec.Parse("daily 00:30");
    var now = new DateTime(2026, 6, 4, 0, 29, 0);
    var lastRun = new DateTime(2026, 6, 3, 0, 30, 0);

    Assert.That(spec.NextDue(lastRun, now), Is.GreaterThan(now));
  }

  [Test]
  public void Given_DailyExactlyAtBoundary_When_ComputingNextDue_Then_Due() {
    var spec = ScheduleSpec.Parse("daily 03:30");
    var now = new DateTime(2026, 6, 4, 3, 30, 0);
    var lastRun = new DateTime(2026, 6, 3, 3, 30, 0);

    Assert.That(spec.NextDue(lastRun, now), Is.EqualTo(now));
  }

  [Test]
  public void Given_DailyMissedSeveralDays_When_ComputingNextDue_Then_OnlyOneCatchUp() {
    var spec = ScheduleSpec.Parse("daily 03:30");
    var now = new DateTime(2026, 6, 4, 12, 0, 0);
    var lastRun = new DateTime(2026, 5, 28, 3, 30, 0);

    var due = spec.NextDue(lastRun, now);

    Assert.That(due, Is.EqualTo(new DateTime(2026, 6, 4, 3, 30, 0)), "the single most recent boundary is due, not every missed one");
  }

  #endregion

  #region NextDue - weekly

  [Test]
  public void Given_WeeklyOnSunday_When_SundayAfterTime_Then_Due() {
    var spec = ScheduleSpec.Parse("weekly Sunday 03:30");
    var now = new DateTime(2026, 6, 7, 4, 0, 0); // 2026-06-07 is a Sunday
    var lastRun = new DateTime(2026, 5, 31, 3, 30, 0);

    Assert.That(spec.NextDue(lastRun, now), Is.EqualTo(new DateTime(2026, 6, 7, 3, 30, 0)));
  }

  [Test]
  public void Given_WeeklyOnSunday_When_Saturday_Then_NotDue() {
    var spec = ScheduleSpec.Parse("weekly Sunday 03:30");
    var now = new DateTime(2026, 6, 6, 12, 0, 0); // Saturday
    var lastRun = new DateTime(2026, 5, 31, 3, 30, 0); // previous Sunday

    Assert.That(spec.NextDue(lastRun, now), Is.GreaterThan(now));
  }

  [Test]
  public void Given_WeeklyMissedTwoWeeks_When_ComputingNextDue_Then_SingleCatchUp() {
    var spec = ScheduleSpec.Parse("weekly Sunday 03:30");
    var now = new DateTime(2026, 6, 7, 4, 0, 0);
    var lastRun = new DateTime(2026, 5, 17, 3, 30, 0); // three Sundays back

    Assert.That(spec.NextDue(lastRun, now), Is.EqualTo(new DateTime(2026, 6, 7, 3, 30, 0)));
  }

  #endregion

  #region clock changes

  [Test]
  public void Given_ClockJumpedBackward_When_AlreadyRan_Then_NotReDue() {
    var spec = ScheduleSpec.Every(TimeSpan.FromHours(1));
    var lastRun = new DateTime(2026, 6, 4, 12, 0, 0);
    var nowAfterJumpBack = new DateTime(2026, 6, 4, 11, 30, 0); // clock moved back 30+ min

    Assert.That(spec.NextDue(lastRun, nowAfterJumpBack), Is.GreaterThan(nowAfterJumpBack));
  }

  [Test]
  public void Given_ClockJumpedForward_When_ComputingNextDue_Then_OneCatchUp() {
    var spec = ScheduleSpec.Every(TimeSpan.FromHours(1));
    var lastRun = new DateTime(2026, 6, 4, 12, 0, 0);
    var nowAfterJump = new DateTime(2026, 6, 5, 12, 0, 0);

    Assert.That(spec.NextDue(lastRun, nowAfterJump), Is.LessThanOrEqualTo(nowAfterJump));
  }

  #endregion

  [Test]
  public void Given_TwoEqualSpecs_When_Comparing_Then_ValueSemanticsHold() {
    var a = ScheduleSpec.Parse("weekly Sun 03:30");
    var b = ScheduleSpec.Parse("weekly Sunday 03:30");
    var c = ScheduleSpec.Parse("weekly Mon 03:30");

    Assert.Multiple(() => {
      Assert.That(a, Is.EqualTo(b));
      Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
      Assert.That(a, Is.Not.EqualTo(c));
      Assert.That(a == b, Is.True);
      Assert.That(a != c, Is.True);
    });
  }

}
