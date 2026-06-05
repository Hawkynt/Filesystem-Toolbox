using Filesystem_Toolbox.Core.Statistics;

namespace Filesystem_Toolbox.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class EventLogTests {

  private DirectoryInfo _testDirectory = null!;
  private EventLog _log = null!;

  [SetUp]
  public void SetUp() {
    this._testDirectory = new(Path.Combine(Path.GetTempPath(), $"FstEventLogTest_{Guid.NewGuid()}"));
    this._testDirectory.Create();
    this._log = new(new FileInfo(Path.Combine(this._testDirectory.FullName, "events.jsonl")));
  }

  [TearDown]
  public void TearDown() {
    if (this._testDirectory.Exists)
      this._testDirectory.Delete(true);
  }

  [Test]
  public void Given_AppendedRecords_When_Reading_Then_AllComeBackInOrder() {
    this._log.Append(EventRecord.Now(@"C:\A", EventType.BitRotFound, @"a.txt"));
    this._log.Append(EventRecord.Now(@"C:\A", EventType.Repaired, @"a.txt"));
    this._log.Append(EventRecord.Now(@"C:\B", EventType.VerifyRun));

    var records = this._log.ReadAll().ToList();

    Assert.Multiple(() => {
      Assert.That(records, Has.Count.EqualTo(3));
      Assert.That(records[0].Type, Is.EqualTo(EventType.BitRotFound));
      Assert.That(records[0].Path, Is.EqualTo(@"a.txt"));
      Assert.That(records[1].Type, Is.EqualTo(EventType.Repaired));
      Assert.That(records[2].Root, Is.EqualTo(@"C:\B"));
    });
  }

  [Test]
  public void Given_ConcurrentAppends_When_Reading_Then_EveryLineIsWellFormed() {
    Parallel.For(0, 200, i => this._log.Append(EventRecord.Now($@"C:\{i % 4}", EventType.Refreshed)));

    Assert.That(this._log.ReadAll().Count(), Is.EqualTo(200), "no torn lines under concurrency");
  }

  [Test]
  public void Given_TornFinalLine_When_Reading_Then_ItIsSkipped() {
    this._log.Append(EventRecord.Now(@"C:\A", EventType.VerifyRun));
    File.AppendAllText(Path.Combine(this._testDirectory.FullName, "events.jsonl"), "{\"utc\":\"2026-");

    Assert.That(this._log.ReadAll().Count(), Is.EqualTo(1));
  }

  [Test]
  public void Given_FileBeyondThreshold_When_Appending_Then_ItRollsAndReadSpansBothSegments() {
    var file = new FileInfo(Path.Combine(this._testDirectory.FullName, "events.jsonl"));
    this._log.Append(EventRecord.Now(@"C:\Old", EventType.VerifyRun));

    // inflate the active file past the threshold without parsing problems (whitespace lines are skipped)
    using (var stream = file.Open(FileMode.Append))
      stream.Write(new byte[EventLog.ROLL_THRESHOLD_BYTES], 0, (int)EventLog.ROLL_THRESHOLD_BYTES);

    this._log.Append(EventRecord.Now(@"C:\New", EventType.VerifyRun));

    Assert.Multiple(() => {
      Assert.That(new FileInfo(Path.Combine(this._testDirectory.FullName, "events.1.jsonl")), Does.Exist);
      var roots = this._log.ReadAll().Select(r => r.Root).ToList();
      Assert.That(roots, Does.Contain(@"C:\Old"), "the rolled segment is still read");
      Assert.That(roots, Does.Contain(@"C:\New"));
    });
  }

}
