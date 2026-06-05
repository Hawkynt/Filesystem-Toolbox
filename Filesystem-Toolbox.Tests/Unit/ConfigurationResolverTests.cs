using Filesystem_Toolbox.Core.Configuration;
using Filesystem_Toolbox.Core.Scheduling;

namespace Filesystem_Toolbox.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class ConfigurationResolverTests {

  // paths are built portably so this fixture also runs on the linux CI leg -
  // the resolver's semantics (deepest wins, case-insensitive, separator boundaries)
  // are OS-neutral even though the product targets Windows
  private static readonly string _BASE = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "FstResolverBase"));

  private static string _P(params string[] parts) => parts.Aggregate(_BASE, Path.Combine);

  private static ConfigurationResolver _Resolver(params WatchedFolderConfiguration[] folders)
    => new(folders);

  [Test]
  public void Given_NoEntries_When_Resolving_Then_HardDefaultsApply() {
    var settings = _Resolver().Resolve(_P("Anywhere", "file.txt"));

    Assert.Multiple(() => {
      Assert.That(settings.ParityRedundancyPercent, Is.EqualTo(ConfigurationDefaults.PARITY_REDUNDANCY_PERCENT));
      Assert.That(settings.AutoRepair, Is.EqualTo(ConfigurationDefaults.AUTO_REPAIR));
      Assert.That(settings.RefreshIntervalDays, Is.EqualTo(ConfigurationDefaults.REFRESH_INTERVAL_DAYS));
      Assert.That(settings.VerifySchedule, Is.EqualTo(ConfigurationDefaults.VERIFY_SCHEDULE));
      Assert.That(settings.BackupPath, Is.Null);
      Assert.That(settings.OnCorruptionCommand, Is.Null);
      Assert.That(settings.GfsKeepDaily, Is.EqualTo(ConfigurationDefaults.GFS_KEEP_DAILY));
      Assert.That(settings.ToastNotifications, Is.EqualTo(ConfigurationDefaults.TOAST_NOTIFICATIONS));
    });
  }

  [Test]
  public void Given_NestedOverride_When_ResolvingBelowIt_Then_DeepestValueWins() {
    var resolver = _Resolver(
      new() { Path = _P("Photos"), ParityRedundancyPercent = 25 },
      new() { Path = _P("Photos", "RAW"), ParityRedundancyPercent = 50 }
    );

    Assert.Multiple(() => {
      Assert.That(resolver.Resolve(_P("Photos", "RAW", "2020", "img.cr2")).ParityRedundancyPercent, Is.EqualTo(50));
      Assert.That(resolver.Resolve(_P("Photos", "vacation.jpg")).ParityRedundancyPercent, Is.EqualTo(25));
    });
  }

  [Test]
  public void Given_NestedEntryWithNullField_When_Resolving_Then_ValueFallsThroughToAncestor() {
    var resolver = _Resolver(
      new() { Path = _P("Photos"), AutoRepair = true, BackupPath = _P("Backups") },
      new() { Path = _P("Photos", "RAW"), ParityRedundancyPercent = 50 } // AutoRepair/BackupPath null here
    );

    var settings = resolver.Resolve(_P("Photos", "RAW", "img.cr2"));

    Assert.Multiple(() => {
      Assert.That(settings.AutoRepair, Is.True, "null falls through to the ancestor");
      Assert.That(settings.BackupPath, Is.EqualTo(_P("Backups")));
      Assert.That(settings.ParityRedundancyPercent, Is.EqualTo(50));
    });
  }

  [Test]
  public void Given_RemovedOverride_When_Resolving_Then_InheritanceChainIsRestored() {
    var withOverride = _Resolver(
      new() { Path = _P("Photos"), RefreshIntervalDays = 100 },
      new() { Path = _P("Photos", "RAW"), RefreshIntervalDays = 0 }
    );
    var withoutOverride = _Resolver(
      new WatchedFolderConfiguration { Path = _P("Photos"), RefreshIntervalDays = 100 }
    );

    Assert.Multiple(() => {
      Assert.That(withOverride.Resolve(_P("Photos", "RAW", "x")).RefreshIntervalDays, Is.Zero);
      Assert.That(withoutOverride.Resolve(_P("Photos", "RAW", "x")).RefreshIntervalDays, Is.EqualTo(100), "removing the entry restores the parent's value");
    });
  }

  [Test]
  public void Given_GlobalVerifySchedule_When_NoFolderSetsOne_Then_GlobalThenDefaultApplies() {
    var global = ScheduleSpec.Parse("daily 04:00");
    var withGlobal = new ConfigurationResolver([new WatchedFolderConfiguration { Path = _P("X") }], global);
    var withoutGlobal = _Resolver(new WatchedFolderConfiguration { Path = _P("X") });

    Assert.Multiple(() => {
      Assert.That(withGlobal.Resolve(_P("X", "f")).VerifySchedule, Is.EqualTo(global));
      Assert.That(withoutGlobal.Resolve(_P("X", "f")).VerifySchedule, Is.EqualTo(ConfigurationDefaults.VERIFY_SCHEDULE));
    });
  }

  [Test]
  public void Given_FolderSchedule_When_Resolving_Then_ItBeatsTheGlobalOne() {
    var resolver = new ConfigurationResolver(
      [new WatchedFolderConfiguration { Path = _P("X"), VerifySchedule = ScheduleSpec.Parse("every 5m") }],
      ScheduleSpec.Parse("daily 04:00")
    );

    Assert.That(resolver.Resolve(_P("X", "f")).VerifySchedule, Is.EqualTo(ScheduleSpec.Every(TimeSpan.FromMinutes(5))));
  }

  [Test]
  public void Given_MixedCasePaths_When_Resolving_Then_MatchingIsCaseInsensitive() {
    var resolver = _Resolver(new WatchedFolderConfiguration { Path = _P("Photos", "RAW"), ParityRedundancyPercent = 50 });

    Assert.That(resolver.Resolve(_P("pHoToS", "raw", "x.bin")).ParityRedundancyPercent, Is.EqualTo(50),
      "the resolver matches case-insensitively by design, regardless of OS");
  }

  [Test]
  public void Given_SiblingWithCommonPrefix_When_Resolving_Then_NoFalseAncestorMatch() {
    var resolver = _Resolver(new WatchedFolderConfiguration { Path = _P("Foo"), ParityRedundancyPercent = 99 });

    Assert.That(resolver.Resolve(_P("FooBar", "x.bin")).ParityRedundancyPercent,
      Is.EqualTo(ConfigurationDefaults.PARITY_REDUNDANCY_PERCENT),
      "FooBar is NOT under Foo");
  }

  [Test]
  public void Given_NestedEntries_When_DeterminingWatchRoots_Then_OnlyTopLevelEntriesQualify() {
    var resolver = _Resolver(
      new() { Path = _P("Photos") },
      new() { Path = _P("Photos", "RAW"), ParityRedundancyPercent = 50 },
      new() { Path = _P("Music") }
    );

    Assert.That(resolver.WatchRoots.Select(r => r.Path), Is.EquivalentTo(new[] { _P("Photos"), _P("Music") }));
  }

  [Test]
  public void Given_EmptyOrWhitespaceStrings_When_Resolving_Then_TheyInheritLikeNull() {
    var resolver = _Resolver(
      new() { Path = _P("Photos"), OnCorruptionCommand = "notify.exe {file}" },
      new() { Path = _P("Photos", "Sub"), OnCorruptionCommand = "   " }
    );

    Assert.That(resolver.Resolve(_P("Photos", "Sub", "f")).OnCorruptionCommand, Is.EqualTo("notify.exe {file}"), "blank text must not shadow the ancestor");
  }

  [Test]
  public void Given_PathOutsideAllEntries_When_Checking_Then_NotCovered() {
    var resolver = _Resolver(new WatchedFolderConfiguration { Path = _P("Photos") });

    Assert.Multiple(() => {
      Assert.That(resolver.IsCovered(_P("Photos", "x")), Is.True);
      Assert.That(resolver.IsCovered(_P("Elsewhere", "x")), Is.False);
    });
  }

  [Test]
  public void Given_DuplicatePathEntries_When_Resolving_Then_FirstOneWins() {
    var resolver = _Resolver(
      new() { Path = _P("Photos"), ParityRedundancyPercent = 10 },
      new() { Path = _P("pHOTOS") + Path.DirectorySeparatorChar, ParityRedundancyPercent = 90 } // same path, different casing/trailing sep
    );

    Assert.That(resolver.Resolve(_P("Photos", "f")).ParityRedundancyPercent, Is.EqualTo(10));
  }

  [Test]
  public void Given_TrailingSeparatorInEntryPath_When_Resolving_Then_NormalizationHandlesIt() {
    var resolver = _Resolver(new WatchedFolderConfiguration { Path = _P("Photos") + Path.DirectorySeparatorChar, AutoRepair = true });

    Assert.That(resolver.Resolve(_P("Photos", "sub", "f.txt")).AutoRepair, Is.True);
  }

  [Test]
  public void Given_QueryForTheEntryPathItself_When_Resolving_Then_EntryApplies() {
    var resolver = _Resolver(new WatchedFolderConfiguration { Path = _P("Photos"), DedupEnabled = true });

    Assert.That(resolver.Resolve(_P("Photos")).DedupEnabled, Is.True);
  }

  [Test]
  public void Given_ThreeLevelChain_When_Resolving_Then_EachSettingTakesItsDeepestNonNull() {
    var resolver = _Resolver(
      new() { Path = _P("A"), ParityRedundancyPercent = 10, AutoRepair = true, RefreshIntervalDays = 100 },
      new() { Path = _P("A", "B"), ParityRedundancyPercent = 20 },
      new() { Path = _P("A", "B", "C"), RefreshIntervalDays = 0 }
    );

    var settings = resolver.Resolve(_P("A", "B", "C", "file.bin"));

    Assert.Multiple(() => {
      Assert.That(settings.ParityRedundancyPercent, Is.EqualTo(20), "from B");
      Assert.That(settings.RefreshIntervalDays, Is.Zero, "from C");
      Assert.That(settings.AutoRepair, Is.True, "from A");
    });
  }

}
