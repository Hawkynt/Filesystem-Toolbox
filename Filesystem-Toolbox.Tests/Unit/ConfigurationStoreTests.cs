using System.Text.Json;
using Filesystem_Toolbox.Core.Configuration;
using Filesystem_Toolbox.Core.Scheduling;

namespace Filesystem_Toolbox.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class ConfigurationStoreTests {

  private DirectoryInfo _testDirectory = null!;

  private FileInfo _JsonFile => new(Path.Combine(this._testDirectory.FullName, "FilesystemToolbox.json"));
  private FileInfo _LegacyFile => new(Path.Combine(this._testDirectory.FullName, "CheckedFolders.lst"));

  [SetUp]
  public void SetUp() {
    this._testDirectory = new(Path.Combine(Path.GetTempPath(), $"FstConfigTest_{Guid.NewGuid()}"));
    this._testDirectory.Create();
  }

  [TearDown]
  public void TearDown() {
    if (this._testDirectory.Exists)
      this._testDirectory.Delete(true);
  }

  [Test]
  public void Given_NoConfigurationFiles_When_Loading_Then_DefaultsAreReturned() {
    var result = ConfigurationStore.Load(this._JsonFile);

    Assert.Multiple(() => {
      Assert.That(result.SchemaVersion, Is.EqualTo(ToolboxConfiguration.CURRENT_SCHEMA_VERSION));
      Assert.That(result.VerifySchedule, Is.Null, "no explicit global schedule - hard default applies downstream");
      Assert.That(result.Folders, Is.Empty);
    });
  }

  [Test]
  public void Given_SavedV2Configuration_When_Loading_Then_AllValuesRoundTrip() {
    var original = new ToolboxConfiguration {
      VerifySchedule = ScheduleSpec.Parse("daily 03:30"),
      Folders = {
        new() {
          Path = @"C:\Watched",
          ParityRedundancyPercent = 50,
          AutoRepair = true,
          BackupPath = @"D:\Backups",
          BackupSchedule = ScheduleSpec.Parse("weekly Sun 02:00"),
          RefreshIntervalDays = 90,
          OnCorruptionCommand = "notify.exe {file} {folder}",
          DedupEnabled = true,
          GfsKeepDaily = 14,
          GfsKeepWeekly = 8,
          GfsKeepMonthly = 24,
          DegradationWarningErrorsPerMonth = 3,
          ToastNotifications = false,
        },
        new() { Path = @"C:\Watched\Sub", ParityRedundancyPercent = 75 }, // partial override entry
      },
    };

    ConfigurationStore.Save(original, this._JsonFile);
    var result = ConfigurationStore.Load(this._JsonFile);

    Assert.Multiple(() => {
      Assert.That(result.VerifySchedule, Is.EqualTo(ScheduleSpec.Parse("daily 03:30")));
      Assert.That(result.Folders, Has.Count.EqualTo(2));
      var f = result.Folders[0];
      Assert.That(f.Path, Is.EqualTo(@"C:\Watched"));
      Assert.That(f.ParityRedundancyPercent, Is.EqualTo(50));
      Assert.That(f.AutoRepair, Is.True);
      Assert.That(f.BackupPath, Is.EqualTo(@"D:\Backups"));
      Assert.That(f.BackupSchedule, Is.EqualTo(ScheduleSpec.Parse("weekly Sunday 02:00")));
      Assert.That(f.RefreshIntervalDays, Is.EqualTo(90));
      Assert.That(f.OnCorruptionCommand, Is.EqualTo("notify.exe {file} {folder}"));
      Assert.That(f.DedupEnabled, Is.True);
      Assert.That(f.GfsKeepDaily, Is.EqualTo(14));
      Assert.That(f.GfsKeepWeekly, Is.EqualTo(8));
      Assert.That(f.GfsKeepMonthly, Is.EqualTo(24));
      Assert.That(f.DegradationWarningErrorsPerMonth, Is.EqualTo(3));
      Assert.That(f.ToastNotifications, Is.False);
      var partial = result.Folders[1];
      Assert.That(partial.ParityRedundancyPercent, Is.EqualTo(75));
      Assert.That(partial.AutoRepair, Is.Null, "unset fields stay null = inherit");
      Assert.That(partial.VerifySchedule, Is.Null);
    });
  }

  [Test]
  public void Given_V1Configuration_When_Loading_Then_MigratedToExplicitV2() {
    File.WriteAllText(this._JsonFile.FullName, """
      {
        "schemaVersion": 1,
        "checkIntervalMinutes": 42,
        "folders": [
          {
            "path": "C:\\Old",
            "parityRedundancyPercent": 30,
            "autoRepair": true,
            "mirrorPath": "E:\\OldMirror",
            "refreshIntervalDays": 60,
            "onCorruptionCommand": "x.exe {file}",
            "dedupEnabled": true
          }
        ]
      }
      """);

    var result = ConfigurationStore.Load(this._JsonFile);

    Assert.Multiple(() => {
      Assert.That(result.SchemaVersion, Is.EqualTo(2));
      Assert.That(result.VerifySchedule, Is.EqualTo(ScheduleSpec.Every(TimeSpan.FromMinutes(42))), "interval becomes the global schedule");
      Assert.That(result.Folders, Has.Count.EqualTo(1));
      var f = result.Folders[0];
      Assert.That(f.ParityRedundancyPercent, Is.EqualTo(30), "v1 values were effective values and stay explicit");
      Assert.That(f.AutoRepair, Is.True);
      Assert.That(f.BackupPath, Is.EqualTo(@"E:\OldMirror"), "mirrorPath becomes backupPath");
      Assert.That(f.RefreshIntervalDays, Is.EqualTo(60));
      Assert.That(f.OnCorruptionCommand, Is.EqualTo("x.exe {file}"));
      Assert.That(f.DedupEnabled, Is.True);
    });
  }

  [Test]
  public void Given_V1Configuration_When_Loading_Then_UpgradedOnDiskWithBackup() {
    File.WriteAllText(this._JsonFile.FullName, """{ "schemaVersion": 1, "checkIntervalMinutes": 10, "folders": [] }""");

    ConfigurationStore.Load(this._JsonFile);

    Assert.Multiple(() => {
      Assert.That(new FileInfo(this._JsonFile.FullName + ".v1.bak"), Does.Exist, "the original v1 file is preserved");
      Assert.That(File.ReadAllText(this._JsonFile.FullName), Does.Contain("\"schemaVersion\": 2"));
      Assert.That(ConfigurationStore.Load(this._JsonFile).SchemaVersion, Is.EqualTo(2), "second load needs no migration");
    });
  }

  [Test]
  public void Given_LegacyListFile_When_Loading_Then_FoldersAreMigratedAsInheritEverything() {
    File.WriteAllLines(this._LegacyFile.FullName, [@"C:\One", "", "   ", @"C:\Two  "]);

    var result = ConfigurationStore.Load(this._JsonFile, this._LegacyFile);

    Assert.Multiple(() => {
      Assert.That(result.Folders.Select(f => f.Path), Is.EqualTo(new[] { @"C:\One", @"C:\Two" }));
      Assert.That(result.Folders, Has.All.Property(nameof(WatchedFolderConfiguration.ParityRedundancyPercent)).Null, "legacy folders inherit everything");
      Assert.That(this._JsonFile, Does.Exist);
      Assert.That(this._LegacyFile, Does.Not.Exist);
      Assert.That(new FileInfo(this._LegacyFile.FullName + ".bak"), Does.Exist);
    });
  }

  [Test]
  public void Given_BothJsonAndLegacyFile_When_Loading_Then_JsonWinsAndLegacyStaysUntouched() {
    ConfigurationStore.Save(new() { VerifySchedule = ScheduleSpec.Parse("every 99m") }, this._JsonFile);
    File.WriteAllLines(this._LegacyFile.FullName, [@"C:\ShouldNotBeMigrated"]);

    var result = ConfigurationStore.Load(this._JsonFile, this._LegacyFile);

    Assert.Multiple(() => {
      Assert.That(result.VerifySchedule, Is.EqualTo(ScheduleSpec.Every(TimeSpan.FromMinutes(99))));
      Assert.That(result.Folders, Is.Empty);
      Assert.That(this._LegacyFile, Does.Exist);
    });
  }

  [Test]
  public void Given_MalformedJson_When_Loading_Then_JsonExceptionIsThrown() {
    File.WriteAllText(this._JsonFile.FullName, "{ this is not json !");

    Assert.That(() => ConfigurationStore.Load(this._JsonFile), Throws.InstanceOf<JsonException>());
  }

  [Test]
  public void Given_NullArguments_When_LoadingOrSaving_Then_ArgumentNullExceptionIsThrown() {
    Assert.Multiple(() => {
      Assert.That(() => ConfigurationStore.Load(null!), Throws.ArgumentNullException);
      Assert.That(() => ConfigurationStore.Save(null!, this._JsonFile), Throws.ArgumentNullException);
      Assert.That(() => ConfigurationStore.Save(new(), null!), Throws.ArgumentNullException);
    });
  }

  [Test]
  public void Given_UnknownJsonProperties_When_Loading_Then_TheyAreIgnored() {
    File.WriteAllText(this._JsonFile.FullName, """{ "schemaVersion": 2, "verifySchedule": "every 5m", "futureSetting": true }""");

    var result = ConfigurationStore.Load(this._JsonFile);

    Assert.That(result.VerifySchedule, Is.EqualTo(ScheduleSpec.Every(TimeSpan.FromMinutes(5))));
  }

  [Test]
  public void Given_NullFields_When_Saving_Then_TheyAreOmittedFromJson() {
    ConfigurationStore.Save(new() { Folders = { new() { Path = @"C:\X" } } }, this._JsonFile);

    var json = File.ReadAllText(this._JsonFile.FullName);
    Assert.Multiple(() => {
      Assert.That(json, Does.Contain("\"path\""));
      Assert.That(json, Does.Not.Contain("parityRedundancyPercent"), "inherit fields are not persisted");
      Assert.That(json, Does.Not.Contain("backupPath"));
    });
  }

}
