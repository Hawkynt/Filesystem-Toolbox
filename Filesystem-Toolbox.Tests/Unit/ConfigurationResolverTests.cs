using Filesystem_Toolbox.Core.Configuration;
using Filesystem_Toolbox.Core.Scheduling;

namespace Filesystem_Toolbox.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class ConfigurationResolverTests {

  private static ConfigurationResolver _Resolver(params WatchedFolderConfiguration[] folders)
    => new(folders);

  [Test]
  public void Given_NoEntries_When_Resolving_Then_HardDefaultsApply() {
    var settings = _Resolver().Resolve(@"C:\Anywhere\file.txt");

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
      new() { Path = @"C:\Photos", ParityRedundancyPercent = 25 },
      new() { Path = @"C:\Photos\RAW", ParityRedundancyPercent = 50 }
    );

    Assert.Multiple(() => {
      Assert.That(resolver.Resolve(@"C:\Photos\RAW\2020\img.cr2").ParityRedundancyPercent, Is.EqualTo(50));
      Assert.That(resolver.Resolve(@"C:\Photos\vacation.jpg").ParityRedundancyPercent, Is.EqualTo(25));
    });
  }

  [Test]
  public void Given_NestedEntryWithNullField_When_Resolving_Then_ValueFallsThroughToAncestor() {
    var resolver = _Resolver(
      new() { Path = @"C:\Photos", AutoRepair = true, BackupPath = @"E:\Backups" },
      new() { Path = @"C:\Photos\RAW", ParityRedundancyPercent = 50 } // AutoRepair/BackupPath null here
    );

    var settings = resolver.Resolve(@"C:\Photos\RAW\img.cr2");

    Assert.Multiple(() => {
      Assert.That(settings.AutoRepair, Is.True, "null falls through to the ancestor");
      Assert.That(settings.BackupPath, Is.EqualTo(@"E:\Backups"));
      Assert.That(settings.ParityRedundancyPercent, Is.EqualTo(50));
    });
  }

  [Test]
  public void Given_RemovedOverride_When_Resolving_Then_InheritanceChainIsRestored() {
    var withOverride = _Resolver(
      new() { Path = @"C:\Photos", RefreshIntervalDays = 100 },
      new() { Path = @"C:\Photos\RAW", RefreshIntervalDays = 0 }
    );
    var withoutOverride = _Resolver(
      new WatchedFolderConfiguration { Path = @"C:\Photos", RefreshIntervalDays = 100 }
    );

    Assert.Multiple(() => {
      Assert.That(withOverride.Resolve(@"C:\Photos\RAW\x").RefreshIntervalDays, Is.Zero);
      Assert.That(withoutOverride.Resolve(@"C:\Photos\RAW\x").RefreshIntervalDays, Is.EqualTo(100), "removing the entry restores the parent's value");
    });
  }

  [Test]
  public void Given_GlobalVerifySchedule_When_NoFolderSetsOne_Then_GlobalThenDefaultApplies() {
    var global = ScheduleSpec.Parse("daily 04:00");
    var withGlobal = new ConfigurationResolver([new WatchedFolderConfiguration { Path = @"C:\X" }], global);
    var withoutGlobal = _Resolver(new WatchedFolderConfiguration { Path = @"C:\X" });

    Assert.Multiple(() => {
      Assert.That(withGlobal.Resolve(@"C:\X\f").VerifySchedule, Is.EqualTo(global));
      Assert.That(withoutGlobal.Resolve(@"C:\X\f").VerifySchedule, Is.EqualTo(ConfigurationDefaults.VERIFY_SCHEDULE));
    });
  }

  [Test]
  public void Given_FolderSchedule_When_Resolving_Then_ItBeatsTheGlobalOne() {
    var resolver = new ConfigurationResolver(
      [new WatchedFolderConfiguration { Path = @"C:\X", VerifySchedule = ScheduleSpec.Parse("every 5m") }],
      ScheduleSpec.Parse("daily 04:00")
    );

    Assert.That(resolver.Resolve(@"C:\X\f").VerifySchedule, Is.EqualTo(ScheduleSpec.Every(TimeSpan.FromMinutes(5))));
  }

  [Test]
  public void Given_MixedCasePaths_When_Resolving_Then_MatchingIsCaseInsensitive() {
    var resolver = _Resolver(new WatchedFolderConfiguration { Path = @"C:\Photos\RAW", ParityRedundancyPercent = 50 });

    Assert.That(resolver.Resolve(@"c:\photos\raw\x.bin").ParityRedundancyPercent, Is.EqualTo(50));
  }

  [Test]
  public void Given_SiblingWithCommonPrefix_When_Resolving_Then_NoFalseAncestorMatch() {
    var resolver = _Resolver(new WatchedFolderConfiguration { Path = @"C:\Foo", ParityRedundancyPercent = 99 });

    Assert.That(resolver.Resolve(@"C:\FooBar\x.bin").ParityRedundancyPercent,
      Is.EqualTo(ConfigurationDefaults.PARITY_REDUNDANCY_PERCENT),
      @"C:\FooBar is NOT under C:\Foo");
  }

  [Test]
  public void Given_NestedEntries_When_DeterminingWatchRoots_Then_OnlyTopLevelEntriesQualify() {
    var resolver = _Resolver(
      new() { Path = @"C:\Photos" },
      new() { Path = @"C:\Photos\RAW", ParityRedundancyPercent = 50 },
      new() { Path = @"D:\Music" }
    );

    Assert.That(resolver.WatchRoots.Select(r => r.Path), Is.EquivalentTo(new[] { @"C:\Photos", @"D:\Music" }));
  }

  [Test]
  public void Given_EmptyOrWhitespaceStrings_When_Resolving_Then_TheyInheritLikeNull() {
    var resolver = _Resolver(
      new() { Path = @"C:\Photos", OnCorruptionCommand = "notify.exe {file}" },
      new() { Path = @"C:\Photos\Sub", OnCorruptionCommand = "   " }
    );

    Assert.That(resolver.Resolve(@"C:\Photos\Sub\f").OnCorruptionCommand, Is.EqualTo("notify.exe {file}"), "blank text must not shadow the ancestor");
  }

  [Test]
  public void Given_PathOutsideAllEntries_When_Checking_Then_NotCovered() {
    var resolver = _Resolver(new WatchedFolderConfiguration { Path = @"C:\Photos" });

    Assert.Multiple(() => {
      Assert.That(resolver.IsCovered(@"C:\Photos\x"), Is.True);
      Assert.That(resolver.IsCovered(@"D:\Elsewhere\x"), Is.False);
    });
  }

  [Test]
  public void Given_DuplicatePathEntries_When_Resolving_Then_FirstOneWins() {
    var resolver = _Resolver(
      new() { Path = @"C:\Photos", ParityRedundancyPercent = 10 },
      new() { Path = @"c:\photos\", ParityRedundancyPercent = 90 } // same path, different casing/trailing sep
    );

    Assert.That(resolver.Resolve(@"C:\Photos\f").ParityRedundancyPercent, Is.EqualTo(10));
  }

  [Test]
  public void Given_TrailingSeparatorInEntryPath_When_Resolving_Then_NormalizationHandlesIt() {
    var resolver = _Resolver(new WatchedFolderConfiguration { Path = @"C:\Photos\", AutoRepair = true });

    Assert.That(resolver.Resolve(@"C:\Photos\sub\f.txt").AutoRepair, Is.True);
  }

  [Test]
  public void Given_QueryForTheEntryPathItself_When_Resolving_Then_EntryApplies() {
    var resolver = _Resolver(new WatchedFolderConfiguration { Path = @"C:\Photos", DedupEnabled = true });

    Assert.That(resolver.Resolve(@"C:\Photos").DedupEnabled, Is.True);
  }

  [Test]
  public void Given_ThreeLevelChain_When_Resolving_Then_EachSettingTakesItsDeepestNonNull() {
    var resolver = _Resolver(
      new() { Path = @"C:\A", ParityRedundancyPercent = 10, AutoRepair = true, RefreshIntervalDays = 100 },
      new() { Path = @"C:\A\B", ParityRedundancyPercent = 20 },
      new() { Path = @"C:\A\B\C", RefreshIntervalDays = 0 }
    );

    var settings = resolver.Resolve(@"C:\A\B\C\file.bin");

    Assert.Multiple(() => {
      Assert.That(settings.ParityRedundancyPercent, Is.EqualTo(20), "from B");
      Assert.That(settings.RefreshIntervalDays, Is.Zero, "from C");
      Assert.That(settings.AutoRepair, Is.True, "from A");
    });
  }

}
