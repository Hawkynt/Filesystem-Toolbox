using Filesystem_Toolbox.Core.Services;

namespace Filesystem_Toolbox.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class GfsRetentionPolicyTests {

  [Test]
  public void Given_ThirtyDailySnapshots_When_Selecting_Then_GfsBucketsSurvive() {
    var policy = new GfsRetentionPolicy(7, 4, 12);
    var times = Enumerable.Range(0, 30).Select(i => new DateTime(2026, 6, 30, 3, 0, 0).AddDays(-i)).ToArray();

    var survivors = policy.SelectSurvivors(times);

    Assert.Multiple(() => {

      // the 7 newest days each keep their snapshot
      foreach (var i in Enumerable.Range(0, 7))
        Assert.That(survivors, Does.Contain(times[i]), $"daily {i}");

      // something older than 7 days must survive through weekly buckets
      Assert.That(survivors.Count(t => t < times[6]), Is.GreaterThan(0), "weekly representatives keep older history");

      // but by no means everything survives
      Assert.That(survivors, Has.Count.LessThan(times.Length));
    });
  }

  [Test]
  public void Given_FewerSnapshotsThanPolicy_When_Selecting_Then_AllSurvive() {
    var policy = new GfsRetentionPolicy(7, 4, 12);
    var times = Enumerable.Range(0, 3).Select(i => new DateTime(2026, 6, 4).AddDays(-i)).ToArray();

    Assert.That(policy.SelectSurvivors(times), Is.EquivalentTo(times));
  }

  [Test]
  public void Given_ZeroPolicy_When_Selecting_Then_NewestStillSurvives() {
    var policy = new GfsRetentionPolicy(0, 0, 0);
    var times = new[] { new DateTime(2026, 6, 4), new DateTime(2026, 6, 3) };

    var survivors = policy.SelectSurvivors(times);

    Assert.Multiple(() => {
      Assert.That(survivors, Does.Contain(new DateTime(2026, 6, 4)), "the newest snapshots are always kept");
      Assert.That(survivors, Has.Count.EqualTo(2), "newest two always survive");
    });
  }

  [Test]
  public void Given_MultipleSnapshotsPerDay_When_Selecting_Then_OnlyTheNewestOfTheDayRepresentsIt() {
    var policy = new GfsRetentionPolicy(2, 0, 0);
    var morning = new DateTime(2026, 6, 4, 8, 0, 0);
    var evening = new DateTime(2026, 6, 4, 20, 0, 0);
    var yesterday = new DateTime(2026, 6, 3, 12, 0, 0);

    var survivors = policy.SelectSurvivors(new[] { morning, evening, yesterday });

    Assert.Multiple(() => {
      Assert.That(survivors, Does.Contain(evening));
      Assert.That(survivors, Does.Contain(yesterday));
      Assert.That(survivors, Does.Contain(morning), "within the newest-two floor everything survives");
    });
  }

  [Test]
  public void Given_SnapshotsAcrossYearBoundary_When_Selecting_Then_IsoWeekBucketsDoNotCollide() {
    var policy = new GfsRetentionPolicy(0, 3, 0);

    // newer anchors occupy the always-keep floor so the week logic is actually exercised:
    // 2025-12-29 (Mon) and 2026-01-04 (Sun) are the SAME ISO week (2026-W01);
    // 2025-12-28 (Sun) is ISO 2025-W52
    var anchor1 = new DateTime(2026, 1, 10);
    var anchor2 = new DateTime(2026, 1, 9);
    var w01a = new DateTime(2025, 12, 29);
    var w01b = new DateTime(2026, 1, 4);
    var w52 = new DateTime(2025, 12, 28);

    var survivors = policy.SelectSurvivors(new[] { anchor1, anchor2, w01a, w01b, w52 });

    Assert.Multiple(() => {
      Assert.That(survivors, Does.Contain(w01b), "newest of ISO week 2026-W01");
      Assert.That(survivors, Does.Not.Contain(w01a), "older snapshot of the same ISO week");
      Assert.That(survivors, Does.Contain(w52), "different ISO week survives");
    });
  }

  [Test]
  public void Given_NegativePolicyValues_When_Constructing_Then_ArgumentOutOfRangeExceptionIsThrown()
    => Assert.That(() => new GfsRetentionPolicy(-1, 4, 12), Throws.InstanceOf<ArgumentOutOfRangeException>());

}
