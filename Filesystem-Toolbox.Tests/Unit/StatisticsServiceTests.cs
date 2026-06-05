using Filesystem_Toolbox.Core.Statistics;

namespace Filesystem_Toolbox.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class StatisticsServiceTests {

  private static readonly DateTime _NOW = new(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);

  private DirectoryInfo _testDirectory = null!;
  private EventLog _log = null!;
  private StatisticsService _service = null!;

  [SetUp]
  public void SetUp() {
    this._testDirectory = new(Path.Combine(Path.GetTempPath(), $"FstStatsTest_{Guid.NewGuid()}"));
    this._testDirectory.Create();
    this._log = new(new FileInfo(Path.Combine(this._testDirectory.FullName, "events.jsonl")));
    this._service = new(this._log, () => _NOW);
  }

  [TearDown]
  public void TearDown() {
    if (this._testDirectory.Exists)
      this._testDirectory.Delete(true);
  }

  private void _Add(EventType type, DateTime utc, string root = @"C:\X", string? detail = null, int? filesChecked = null)
    => this._log.Append(new EventRecord { Utc = utc, Root = root, Type = type, Detail = detail, FilesChecked = filesChecked });

  [Test]
  public void Given_MixedEvents_When_ComputingKpis_Then_WindowsAreCorrect() {
    this._Add(EventType.BitRotFound, _NOW.AddDays(-1));     // 7d, 30d, total
    this._Add(EventType.BitRotFound, _NOW.AddDays(-10));    // 30d, total
    this._Add(EventType.BitRotFound, _NOW.AddDays(-100));   // total only
    this._Add(EventType.Repaired, _NOW.AddDays(-1));
    this._Add(EventType.RepairedFromBackup, _NOW.AddDays(-50));
    this._Add(EventType.BitRotFound, _NOW.AddDays(-2), root: @"C:\Other"); // different root - ignored

    var stats = this._service.For(@"C:\X", 5);

    Assert.Multiple(() => {
      Assert.That(stats.ErrorsFoundTotal, Is.EqualTo(3));
      Assert.That(stats.ErrorsFound30d, Is.EqualTo(2));
      Assert.That(stats.ErrorsFound7d, Is.EqualTo(1));
      Assert.That(stats.ErrorsCorrectedTotal, Is.EqualTo(2));
      Assert.That(stats.ErrorsCorrected30d, Is.EqualTo(1));
      Assert.That(stats.ErrorsCorrected7d, Is.EqualTo(1));
    });
  }

  [Test]
  public void Given_FewerThanTwoFailures_When_ComputingMtbf_Then_NoFailuresYet() {
    this._Add(EventType.BitRotFound, _NOW.AddDays(-1));

    var stats = this._service.For(@"C:\X", 5);

    Assert.Multiple(() => {
      Assert.That(stats.MeanTimeBetweenFailures, Is.Null);
      Assert.That(stats.MeanTimeBetweenFailuresHuman, Is.EqualTo("no failures yet"));
    });
  }

  [Test]
  public void Given_EvenlySpacedFailures_When_ComputingMtbf_Then_MeanGapResults() {
    this._Add(EventType.BitRotFound, _NOW.AddDays(-20));
    this._Add(EventType.BitRotFound, _NOW.AddDays(-10));
    this._Add(EventType.BitRotFound, _NOW);

    var stats = this._service.For(@"C:\X", 5);

    Assert.Multiple(() => {
      Assert.That(stats.MeanTimeBetweenFailures, Is.EqualTo(TimeSpan.FromDays(10)));
      Assert.That(stats.MeanTimeBetweenFailuresHuman, Is.EqualTo("10 days"));
    });
  }

  [Test]
  public void Given_LongGaps_When_FormattingMtbf_Then_MonthsAreUsed() {
    this._Add(EventType.BitRotFound, _NOW.AddDays(-400));
    this._Add(EventType.BitRotFound, _NOW);

    Assert.That(this._service.For(@"C:\X", 5).MeanTimeBetweenFailuresHuman, Does.EndWith("months"));
  }

  [TestCase(0, DegradationStatus.Healthy)]
  [TestCase(4, DegradationStatus.Healthy)]
  [TestCase(5, DegradationStatus.Degrading)]
  [TestCase(14, DegradationStatus.Degrading)]
  [TestCase(15, DegradationStatus.Failing, Description = "3x threshold")]
  public void Given_ErrorsThisMonth_When_Classifying_Then_ThresholdTiersApply(int errorsThisMonth, DegradationStatus expected) {
    for (var i = 0; i < errorsThisMonth; ++i)
      this._Add(EventType.BitRotFound, new DateTime(_NOW.Year, _NOW.Month, 1, 6, 0, 0, DateTimeKind.Utc).AddMinutes(i));

    Assert.That(this._service.For(@"C:\X", 5).Degradation, Is.EqualTo(expected));
  }

  [Test]
  public void Given_RecentUnrepairable_When_Classifying_Then_FailingRegardlessOfCount() {
    this._Add(EventType.Unrepairable, _NOW.AddDays(-5));

    Assert.That(this._service.For(@"C:\X", 5).Degradation, Is.EqualTo(DegradationStatus.Failing));
  }

  [Test]
  public void Given_TwelveMonthsOfErrors_When_Bucketing_Then_EachMonthCountsItsOwn() {
    this._Add(EventType.BitRotFound, _NOW.AddMonths(-1));
    this._Add(EventType.BitRotFound, _NOW.AddMonths(-1).AddHours(1));
    this._Add(EventType.Repaired, _NOW.AddMonths(-2));

    var byMonth = this._service.For(@"C:\X", 5).ByMonthLast12;

    Assert.Multiple(() => {
      Assert.That(byMonth, Has.Count.EqualTo(12));
      Assert.That(byMonth[10].Found, Is.EqualTo(2), "previous month holds both findings");
      Assert.That(byMonth[9].Corrected, Is.EqualTo(1));
      Assert.That(byMonth[11].Found, Is.Zero, "current month is clean");
    });
  }

  [Test]
  public void Given_VerifyRunWithDetail_When_ReadingDistribution_Then_PieDataResults() {
    this._Add(EventType.VerifyRun, _NOW.AddHours(-1), detail: "BitRot=2;Modified=1", filesChecked: 50);

    var distribution = this._service.For(@"C:\X", 5).LastVerifyDistribution;

    Assert.Multiple(() => {
      Assert.That(distribution["BitRot"], Is.EqualTo(2));
      Assert.That(distribution["Modified"], Is.EqualTo(1));
      Assert.That(distribution["Ok"], Is.EqualTo(47), "Ok = checked minus problems");
    });
  }

  [Test]
  public void Given_ThresholdReached_When_CheckingCrossing_Then_TrueOnceThenSuppressedByWarningEvent() {
    for (var i = 0; i < 5; ++i)
      this._Add(EventType.BitRotFound, _NOW.AddHours(-i - 1));

    Assert.That(this._service.CrossedThresholdToday(@"C:\X", 5), Is.True);

    this._Add(EventType.DeviceWarning, _NOW.AddMinutes(-5));

    Assert.That(this._service.CrossedThresholdToday(@"C:\X", 5), Is.False, "warned already today");
  }

  [Test]
  public void Given_ErrorsBelowThreshold_When_CheckingCrossing_Then_False() {
    this._Add(EventType.BitRotFound, _NOW.AddHours(-1));

    Assert.That(this._service.CrossedThresholdToday(@"C:\X", 5), Is.False);
  }

  [Test]
  public void Given_EventsFromSeveralRoots_When_ListingKnownRoots_Then_AllDistinctRootsAppear() {
    this._Add(EventType.VerifyRun, _NOW, root: @"C:\A");
    this._Add(EventType.VerifyRun, _NOW, root: @"C:\B");
    this._Add(EventType.VerifyRun, _NOW, root: @"c:\a");

    Assert.That(this._service.KnownRoots(), Has.Count.EqualTo(2));
  }

}
