using Filesystem_Toolbox.Core.Configuration;
using Filesystem_Toolbox.Core.Scheduling;

namespace Filesystem_Toolbox.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class SchedulerServiceTests {

  private DirectoryInfo _testDirectory = null!;
  private DateTime _now;

  private FileInfo _StateFile => new(Path.Combine(this._testDirectory.FullName, "SchedulerState.json"));

  [SetUp]
  public void SetUp() {
    this._testDirectory = new(Path.Combine(Path.GetTempPath(), $"FstSchedulerTest_{Guid.NewGuid()}"));
    this._testDirectory.Create();
    this._now = new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Local);
  }

  [TearDown]
  public void TearDown() {
    if (this._testDirectory.Exists)
      this._testDirectory.Delete(true);
  }

  private SchedulerService _Service() => new(this._StateFile, () => this._now);

  private static ConfigurationResolver _Resolver(params WatchedFolderConfiguration[] folders) => new(folders);

  private static WatchedFolderConfiguration _Folder(string path, string? verify = "every 10m", string? backupPath = null, string? backupSchedule = null, int refreshDays = 0) => new() {
    Path = path,
    VerifySchedule = verify == null ? null : ScheduleSpec.Parse(verify),
    BackupPath = backupPath,
    BackupSchedule = backupSchedule == null ? null : ScheduleSpec.Parse(backupSchedule),
    RefreshIntervalDays = refreshDays,
  };

  [Test]
  public void Given_NoWatchRoots_When_Polling_Then_NothingIsDue()
    => Assert.That(this._Service().GetDueActions(_Resolver()), Is.Empty);

  [Test]
  public void Given_FreshState_When_Polling_Then_VerifyIsDueImmediately() {
    var due = this._Service().GetDueActions(_Resolver(_Folder(@"C:\X")));

    Assert.Multiple(() => {
      Assert.That(due, Has.Count.EqualTo(1));
      Assert.That(due[0].Action, Is.EqualTo(ScheduledAction.Verify));
      Assert.That(due[0].RootPath, Is.EqualTo(@"C:\X"));
    });
  }

  [Test]
  public void Given_CompletedRun_When_PollingAgainWithinInterval_Then_NotDue() {
    var service = this._Service();
    var resolver = _Resolver(_Folder(@"C:\X", verify: "every 10m"));
    var due = service.GetDueActions(resolver)[0];
    Assert.That(service.TryBeginRun(due), Is.True);
    service.CompleteRun(due);

    this._now = this._now.AddMinutes(5);

    Assert.That(service.GetDueActions(resolver), Is.Empty);
  }

  [Test]
  public void Given_CompletedRun_When_IntervalElapses_Then_DueAgain() {
    var service = this._Service();
    var resolver = _Resolver(_Folder(@"C:\X", verify: "every 10m"));
    var due = service.GetDueActions(resolver)[0];
    service.TryBeginRun(due);
    service.CompleteRun(due);

    this._now = this._now.AddMinutes(10);

    Assert.That(service.GetDueActions(resolver), Has.Count.EqualTo(1), "due exactly at the boundary");
  }

  [Test]
  public void Given_AppWasOffPastTheBoundary_When_Restarting_Then_OneCatchUpIsDue() {
    var resolver = _Resolver(_Folder(@"C:\X", verify: "daily 03:30"));

    // first lifetime: complete a run yesterday morning
    var first = this._Service();
    this._now = new DateTime(2026, 6, 3, 3, 31, 0);
    var due = first.GetDueActions(resolver)[0];
    first.TryBeginRun(due);
    first.CompleteRun(due);

    // app restarts well past the next boundary - state file persisted the last run
    this._now = new DateTime(2026, 6, 4, 12, 0, 0);
    var second = this._Service();

    Assert.That(second.GetDueActions(resolver), Has.Count.EqualTo(1), "the missed daily window is caught up once");
  }

  [Test]
  public void Given_RunningAction_When_Polling_Then_ItIsNotOfferedAgain() {
    var service = this._Service();
    var resolver = _Resolver(_Folder(@"C:\X"));
    var due = service.GetDueActions(resolver)[0];

    Assert.Multiple(() => {
      Assert.That(service.TryBeginRun(due), Is.True);
      Assert.That(service.TryBeginRun(due), Is.False, "no double-claim");
      Assert.That(service.GetDueActions(resolver), Is.Empty, "in-flight actions are excluded");
    });
  }

  [Test]
  public void Given_AbortedRun_When_Polling_Then_ItStaysDueForRetry() {
    var service = this._Service();
    var resolver = _Resolver(_Folder(@"C:\X"));
    var due = service.GetDueActions(resolver)[0];
    service.TryBeginRun(due);

    service.AbortRun(due);

    Assert.That(service.GetDueActions(resolver), Has.Count.EqualTo(1), "failures are retried");
  }

  [Test]
  public void Given_BackupConfiguration_When_Polling_Then_BackupOnlyDueWithPathAndSchedule() {
    var service = this._Service();

    var withBoth = _Resolver(_Folder(@"C:\A", verify: null, backupPath: @"E:\Bak", backupSchedule: "every 1h"));
    var pathOnly = _Resolver(_Folder(@"C:\B", verify: null, backupPath: @"E:\Bak"));
    var scheduleOnly = _Resolver(_Folder(@"C:\C", verify: null, backupSchedule: "every 1h"));

    Assert.Multiple(() => {
      Assert.That(withBoth.WatchRoots, Has.Count.EqualTo(1));
      Assert.That(service.GetDueActions(withBoth).Select(d => d.Action), Does.Contain(ScheduledAction.Backup));
      Assert.That(service.GetDueActions(pathOnly).Select(d => d.Action), Does.Not.Contain(ScheduledAction.Backup), "no schedule = manual backups only");
      Assert.That(service.GetDueActions(scheduleOnly).Select(d => d.Action), Does.Not.Contain(ScheduledAction.Backup), "no target = nothing to back up to");
    });
  }

  [Test]
  public void Given_RefreshIntervalDays_When_Polling_Then_RefreshDueOnlyWhenPositive() {
    var service = this._Service();

    var enabled = service.GetDueActions(_Resolver(_Folder(@"C:\A", refreshDays: 30))).Select(d => d.Action);
    var disabled = service.GetDueActions(_Resolver(_Folder(@"C:\B", refreshDays: 0))).Select(d => d.Action);

    Assert.Multiple(() => {
      Assert.That(enabled, Does.Contain(ScheduledAction.Refresh));
      Assert.That(disabled, Does.Not.Contain(ScheduledAction.Refresh));
    });
  }

  [Test]
  public void Given_TwoRoots_When_OneCompletes_Then_TheOtherStaysIndependentlyDue() {
    var service = this._Service();
    var resolver = _Resolver(_Folder(@"C:\A"), _Folder(@"C:\B"));
    var due = service.GetDueActions(resolver);
    Assert.That(due, Has.Count.EqualTo(2));

    service.TryBeginRun(due[0]);
    service.CompleteRun(due[0]);

    var remaining = service.GetDueActions(resolver);
    Assert.Multiple(() => {
      Assert.That(remaining, Has.Count.EqualTo(1));
      Assert.That(remaining[0].RootPath, Is.Not.EqualTo(due[0].RootPath));
    });
  }

  [Test]
  public void Given_CorruptStateFile_When_Constructing_Then_FreshStateWithoutThrowing() {
    File.WriteAllText(this._StateFile.FullName, "{ not json !");

    Assert.That(() => this._Service().GetDueActions(_Resolver(_Folder(@"C:\X"))), Throws.Nothing);
  }

  [Test]
  public void Given_ClockMovedBackward_When_AlreadyCompleted_Then_NotReDue() {
    var service = this._Service();
    var resolver = _Resolver(_Folder(@"C:\X", verify: "every 1h"));
    var due = service.GetDueActions(resolver)[0];
    service.TryBeginRun(due);
    service.CompleteRun(due);

    this._now = this._now.AddMinutes(-30);

    Assert.That(service.GetDueActions(resolver), Is.Empty);
  }

}
