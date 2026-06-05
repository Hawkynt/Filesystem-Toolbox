using System.Text.Json;
using Filesystem_Toolbox.Core.Configuration;

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
      Assert.That(result.CheckIntervalMinutes, Is.EqualTo(10));
      Assert.That(result.Folders, Is.Empty);
    });
  }

  [Test]
  public void Given_SavedConfiguration_When_Loading_Then_AllValuesRoundTrip() {
    var original = new ToolboxConfiguration {
      CheckIntervalMinutes = 42,
      Folders = {
        new() {
          Path = @"C:\Watched",
          ParityRedundancyPercent = 50,
          AutoRepair = true,
          MirrorPath = @"D:\Mirror",
          RefreshIntervalDays = 90,
          OnCorruptionCommand = "notify.exe {file} {folder}",
          DedupEnabled = true,
        },
      },
    };

    ConfigurationStore.Save(original, this._JsonFile);
    var result = ConfigurationStore.Load(this._JsonFile);

    Assert.Multiple(() => {
      Assert.That(result.CheckIntervalMinutes, Is.EqualTo(42));
      Assert.That(result.Folders, Has.Count.EqualTo(1));
      Assert.That(result.Folders[0].Path, Is.EqualTo(@"C:\Watched"));
      Assert.That(result.Folders[0].ParityRedundancyPercent, Is.EqualTo(50));
      Assert.That(result.Folders[0].AutoRepair, Is.True);
      Assert.That(result.Folders[0].MirrorPath, Is.EqualTo(@"D:\Mirror"));
      Assert.That(result.Folders[0].RefreshIntervalDays, Is.EqualTo(90));
      Assert.That(result.Folders[0].OnCorruptionCommand, Is.EqualTo("notify.exe {file} {folder}"));
      Assert.That(result.Folders[0].DedupEnabled, Is.True);
    });
  }

  [Test]
  public void Given_LegacyListFile_When_Loading_Then_FoldersAreMigratedWithDefaultPolicies() {
    File.WriteAllLines(this._LegacyFile.FullName, [@"C:\One", "", "   ", @"C:\Two  "]);

    var result = ConfigurationStore.Load(this._JsonFile, this._LegacyFile);

    Assert.Multiple(() => {
      Assert.That(result.Folders.Select(f => f.Path), Is.EqualTo(new[] { @"C:\One", @"C:\Two" }));
      Assert.That(result.Folders, Has.All.Property(nameof(WatchedFolderConfiguration.ParityRedundancyPercent)).EqualTo(25));
      Assert.That(result.Folders, Has.All.Property(nameof(WatchedFolderConfiguration.AutoRepair)).False);
      Assert.That(result.Folders, Has.All.Property(nameof(WatchedFolderConfiguration.RefreshIntervalDays)).EqualTo(180));
    });
  }

  [Test]
  public void Given_LegacyListFile_When_Loading_Then_JsonIsWrittenAndLegacyRenamedToBak() {
    File.WriteAllLines(this._LegacyFile.FullName, [@"C:\One"]);

    ConfigurationStore.Load(this._JsonFile, this._LegacyFile);

    Assert.Multiple(() => {
      Assert.That(this._JsonFile, Does.Exist);
      Assert.That(this._LegacyFile, Does.Not.Exist);
      Assert.That(new FileInfo(this._LegacyFile.FullName + ".bak"), Does.Exist);
    });
  }

  [Test]
  public void Given_BothJsonAndLegacyFile_When_Loading_Then_JsonWinsAndLegacyStaysUntouched() {
    ConfigurationStore.Save(new() { CheckIntervalMinutes = 99 }, this._JsonFile);
    File.WriteAllLines(this._LegacyFile.FullName, [@"C:\ShouldNotBeMigrated"]);

    var result = ConfigurationStore.Load(this._JsonFile, this._LegacyFile);

    Assert.Multiple(() => {
      Assert.That(result.CheckIntervalMinutes, Is.EqualTo(99));
      Assert.That(result.Folders, Is.Empty);
      Assert.That(this._LegacyFile, Does.Exist);
    });
  }

  [Test]
  public void Given_EmptyLegacyFile_When_Loading_Then_NoFoldersButMigrationStillHappens() {
    File.WriteAllText(this._LegacyFile.FullName, string.Empty);

    var result = ConfigurationStore.Load(this._JsonFile, this._LegacyFile);

    Assert.Multiple(() => {
      Assert.That(result.Folders, Is.Empty);
      Assert.That(this._JsonFile, Does.Exist);
      Assert.That(this._LegacyFile, Does.Not.Exist);
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
    File.WriteAllText(this._JsonFile.FullName, """{ "checkIntervalMinutes": 5, "futureSetting": true }""");

    var result = ConfigurationStore.Load(this._JsonFile);

    Assert.That(result.CheckIntervalMinutes, Is.EqualTo(5));
  }

}
