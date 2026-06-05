using Filesystem_Toolbox.Core.Integrity;

namespace Filesystem_Toolbox.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class DatabaseParityGuardTests {

  private DirectoryInfo _root = null!;
  private FileInfo _databaseFile = null!;
  private DatabaseParityGuard _guard = null!;

  [SetUp]
  public void SetUp() {
    this._root = new(Path.Combine(Path.GetTempPath(), $"FstDbGuardTest_{Guid.NewGuid()}"));
    this._root.Create();
    this._databaseFile = new(Path.Combine(this._root.FullName, "checksum.db"));
    this._guard = new(this._root);
  }

  [TearDown]
  public void TearDown() {
    if (!this._root.Exists)
      return;

    foreach (var file in this._root.EnumerateFiles("*", SearchOption.AllDirectories))
      file.Attributes = FileAttributes.Normal;

    this._root.Delete(true);
  }

  private void _WriteDatabase(string content) {
    File.WriteAllText(this._databaseFile.FullName, content);
    this._databaseFile.Refresh();
  }

  /// <summary>Flips a byte while preserving the modification time - silent rot.</summary>
  private void _Rot(long offset) {
    this._databaseFile.Refresh();
    var mtime = this._databaseFile.LastWriteTimeUtc;
    using (var stream = this._databaseFile.Open(FileMode.Open, FileAccess.ReadWrite)) {
      stream.Position = offset;
      var b = stream.ReadByte();
      stream.Position = offset;
      stream.WriteByte((byte)(b ^ 0xFF));
    }

    File.SetLastWriteTimeUtc(this._databaseFile.FullName, mtime);
  }

  [Test]
  public void Given_ProtectedDatabase_When_Unchanged_Then_HealthyAndHealNotNeeded() {
    this._WriteDatabase("100:1:abc= => a.txt\n");
    this._guard.Protect(this._databaseFile);

    Assert.Multiple(() => {
      Assert.That(this._guard.IsHealthy(this._databaseFile), Is.True);
      Assert.That(this._guard.Heal(this._databaseFile), Is.EqualTo(DbHealResult.NotNeeded));
    });
  }

  [Test]
  public void Given_RottenDatabase_When_Healing_Then_ContentIsRestored() {
    var content = string.Join("\n", Enumerable.Range(0, 100).Select(i => $"{i}:1:hash{i}= => file{i}.txt"));
    this._WriteDatabase(content);
    this._guard.Protect(this._databaseFile);
    this._Rot(50);

    Assert.Multiple(() => {
      Assert.That(this._guard.IsHealthy(this._databaseFile), Is.False);
      Assert.That(this._guard.Heal(this._databaseFile), Is.EqualTo(DbHealResult.Repaired));
      Assert.That(File.ReadAllText(this._databaseFile.FullName), Is.EqualTo(content), "the database must come back bit for bit");
      Assert.That(this._guard.IsHealthy(this._databaseFile), Is.True);
    });
  }

  [Test]
  public void Given_DatabaseSavedAfterLastParityBuild_When_Healing_Then_ParityReboundNotRegressed() {
    this._WriteDatabase("old content\n");
    this._guard.Protect(this._databaseFile);

    // a NEWER legitimate save that the debounced parity build never caught (e.g. shutdown)
    Thread.Sleep(50);
    this._WriteDatabase("newer content that must survive\n");

    var result = this._guard.Heal(this._databaseFile);

    Assert.Multiple(() => {
      Assert.That(result, Is.EqualTo(DbHealResult.ParityRebuilt), "stale parity must rebind, never regress");
      Assert.That(File.ReadAllText(this._databaseFile.FullName), Is.EqualTo("newer content that must survive\n"));
      Assert.That(this._guard.IsHealthy(this._databaseFile), Is.True, "parity is now bound to the newer content");
    });
  }

  [Test]
  public void Given_NoParity_When_CheckingHealth_Then_HealthyWithoutFalseAlarm() {
    this._WriteDatabase("anything\n");

    Assert.Multiple(() => {
      Assert.That(this._guard.IsHealthy(this._databaseFile), Is.True);
      Assert.That(this._guard.Heal(this._databaseFile), Is.EqualTo(DbHealResult.NotNeeded));
    });
  }

  [Test]
  public void Given_CorruptParityHeader_When_CheckingHealth_Then_SafeFallthrough() {
    this._WriteDatabase("data\n");
    this._guard.Protect(this._databaseFile);
    File.WriteAllBytes(this._guard.GetParityFile(this._databaseFile).FullName, new byte[20]);

    Assert.That(this._guard.IsHealthy(this._databaseFile), Is.True, "an unreadable parity cannot raise alarms");
  }

  [Test]
  public void Given_CheckerWithRottenDatabase_When_Loading_Then_DatabaseIsHealedAndEventRaised() {
    DbHealResult? observed = null;

    // build a tracked db + its parity through the real checker machinery
    var dataFile = new FileInfo(Path.Combine(this._root.FullName, "tracked.txt"));
    File.WriteAllText(dataFile.FullName, "important payload");
    using (var checker = new FolderIntegrityChecker(this._root)) {
      checker.UpdateFile(dataFile);
      checker.SaveDatabase();
    }

    // the dispose-save schedules a debounced parity build; force it deterministically
    this._guard.Protect(new FileInfo(Path.Combine(this._root.FullName, "checksum.db")));

    // rot the database
    this._databaseFile.Refresh();
    this._databaseFile.Attributes = FileAttributes.Normal;
    this._Rot(10);

    using (var reloaded = new FolderIntegrityChecker(this._root)) {
      reloaded.DatabaseHealed += (_, result) => observed = result;
      reloaded.LoadDatabase();

      Assert.Multiple(() => {
        Assert.That(observed, Is.EqualTo(DbHealResult.Repaired));
        Assert.That(reloaded.TryGetEntry(dataFile, out _), Is.True, "the healed database parses and still knows its files");
      });
    }
  }

}
