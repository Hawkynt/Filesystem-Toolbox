using Filesystem_Toolbox.Core.Integrity;
using Filesystem_Toolbox.Core.Redundancy;
using Filesystem_Toolbox.Core.Services;

namespace Filesystem_Toolbox.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class RepairServiceTests {

  private const int _SHARD = 64 * 1024;

  private DirectoryInfo _root = null!;
  private DirectoryInfo _backupRoot = null!;
  private FolderIntegrityChecker _checker = null!;
  private ParityStore _parityStore = null!;

  [SetUp]
  public void SetUp() {
    this._root = new(Path.Combine(Path.GetTempPath(), $"FstRepairTest_{Guid.NewGuid()}"));
    this._root.Create();
    this._backupRoot = new(Path.Combine(Path.GetTempPath(), $"FstRepairBackup_{Guid.NewGuid()}"));
    this._backupRoot.Create();
    this._checker = new(this._root);
    this._parityStore = new(this._root, 25); // m = 4 parity shards per stripe
  }

  [TearDown]
  public void TearDown() {
    this._checker.Dispose();
    foreach (var dir in new[] { this._root, this._backupRoot }) {
      dir.Refresh();
      if (!dir.Exists)
        continue;

      foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
        file.Attributes = FileAttributes.Normal;

      foreach (var sub in dir.EnumerateDirectories("*", SearchOption.AllDirectories))
        sub.Attributes = FileAttributes.Directory;

      dir.Delete(true);
    }
  }

  /// <summary>Creates a tracked, parity-protected file with deterministic content.</summary>
  private FileInfo _CreateProtectedFile(string name, int length, int seed = 7) {
    var data = new byte[length];
    new Random(seed).NextBytes(data);
    var file = new FileInfo(Path.Combine(this._root.FullName, name));
    file.Directory!.Create();
    File.WriteAllBytes(file.FullName, data);
    this._checker.UpdateFile(file);
    this._parityStore.BuildParity(file);
    return file;
  }

  /// <summary>Flips bytes while restoring the modification time - the bit-rot signature.</summary>
  private static void _Corrupt(FileInfo file, params long[] offsets) {
    file.Refresh();
    var mtime = file.LastWriteTimeUtc;
    using (var stream = file.Open(FileMode.Open, FileAccess.ReadWrite)) {
      foreach (var offset in offsets) {
        stream.Position = offset;
        var b = stream.ReadByte();
        stream.Position = offset;
        stream.WriteByte((byte)(b ^ 0xFF));
      }
    }

    File.SetLastWriteTimeUtc(file.FullName, mtime);
  }

  private BackupService _Backup() => new(this._checker, this._backupRoot, GfsRetentionPolicy.Default);

  private RepairService _Service(bool withBackup = false)
    => new(this._checker, this._parityStore, withBackup ? this._Backup() : null);

  private static byte[] _ExpectedContent(int length, int seed = 7) {
    var data = new byte[length];
    new Random(seed).NextBytes(data);
    return data;
  }

  [Test]
  public void Given_HealthyFile_When_Repairing_Then_NotNeeded() {
    var file = this._CreateProtectedFile("ok.bin", 100_000);

    Assert.That(this._Service().Repair(file).Result, Is.EqualTo(RepairResult.NotNeeded));
  }

  [Test]
  public void Given_BitRotInOneShard_When_Repairing_Then_FileIsRestoredExactly() {
    var file = this._CreateProtectedFile("rot.bin", 300_000);
    file.Refresh();
    var originalMtime = file.LastWriteTimeUtc;
    _Corrupt(file, 10, 1000, 2000); // three flips inside shard 0

    var outcome = this._Service().Repair(file);

    file.Refresh();
    Assert.Multiple(() => {
      Assert.That(outcome.Result, Is.EqualTo(RepairResult.Repaired));
      Assert.That(outcome.StripesRepaired, Is.EqualTo(1));
      Assert.That(File.ReadAllBytes(file.FullName), Is.EqualTo(_ExpectedContent(300_000)), "content must match the original bit for bit");
      Assert.That(file.LastWriteTimeUtc, Is.EqualTo(originalMtime), "repairs must be invisible to metadata");
    });
  }

  [Test]
  public void Given_BitRotInExactlyMShards_When_Repairing_Then_FileIsRestored() {
    var file = this._CreateProtectedFile("rot4.bin", 5 * _SHARD); // spans 5 shards of stripe 0
    _Corrupt(file, 0 * _SHARD + 5, 1 * _SHARD + 5, 2 * _SHARD + 5, 3 * _SHARD + 5); // 4 = m damaged shards

    var outcome = this._Service().Repair(file);

    Assert.Multiple(() => {
      Assert.That(outcome.Result, Is.EqualTo(RepairResult.Repaired));
      Assert.That(outcome.BadShardsFound, Is.EqualTo(4));
      Assert.That(File.ReadAllBytes(file.FullName), Is.EqualTo(_ExpectedContent(5 * _SHARD)));
    });
  }

  [Test]
  public void Given_MoreDamageThanParity_When_RepairingWithoutBackup_Then_Unrepairable() {
    var file = this._CreateProtectedFile("rot5.bin", 5 * _SHARD);
    _Corrupt(file, 0 * _SHARD + 5, 1 * _SHARD + 5, 2 * _SHARD + 5, 3 * _SHARD + 5, 4 * _SHARD + 5); // 5 > m

    var outcome = this._Service().Repair(file);

    Assert.That(outcome.Result, Is.EqualTo(RepairResult.Unrepairable));
  }

  [Test]
  public void Given_MoreDamageThanParity_When_RepairingWithGoodBackup_Then_RestoredFromBackup() {
    var file = this._CreateProtectedFile("rot5m.bin", 5 * _SHARD);
    this._Backup().RunBackup(); // good snapshot exists
    _Corrupt(file, 0 * _SHARD + 5, 1 * _SHARD + 5, 2 * _SHARD + 5, 3 * _SHARD + 5, 4 * _SHARD + 5);

    var outcome = this._Service(withBackup: true).Repair(file);

    Assert.Multiple(() => {
      Assert.That(outcome.Result, Is.EqualTo(RepairResult.RepairedFromBackup));
      Assert.That(File.ReadAllBytes(file.FullName), Is.EqualTo(_ExpectedContent(5 * _SHARD)));
    });
  }

  [Test]
  public void Given_CorruptParityShardWithinCapacity_When_Repairing_Then_RepairedAndParitySelfHealed() {
    var file = this._CreateProtectedFile("rotp.bin", 300_000);
    _Corrupt(file, 5); // one bad data shard

    // additionally damage one parity shard (still 2 erasures <= m=4)
    var parityFile = this._parityStore.GetParityFile(file);
    var payloadOffset = ParityFileFormat.GetPayloadOffset(1, 16, 4);
    using (var stream = parityFile.Open(FileMode.Open, FileAccess.ReadWrite)) {
      stream.Position = payloadOffset + 7;
      var b = stream.ReadByte();
      stream.Position = payloadOffset + 7;
      stream.WriteByte((byte)(b ^ 0xAA));
    }

    var outcome = this._Service().Repair(file);

    Assert.Multiple(() => {
      Assert.That(outcome.Result, Is.EqualTo(RepairResult.Repaired));
      Assert.That(File.ReadAllBytes(file.FullName), Is.EqualTo(_ExpectedContent(300_000)));
      using var reader = ParityFileReader.Open(this._parityStore.GetParityFile(file));
      Assert.That(reader.VerifyPayloadCrc(), Is.True, "damaged parity must have been rebuilt from the healed data");
    });
  }

  [Test]
  public void Given_StaleParity_When_RepairingBitRot_Then_StaleParityIsRefused() {

    // parity bound to version 1, database accepted version 2, then version 2 rots:
    // using the old parity would "repair" towards outdated content - data loss
    var file = this._CreateProtectedFile("stale.bin", 100_000);
    File.WriteAllBytes(file.FullName, _ExpectedContent(100_000, seed: 99)); // legit edit
    this._checker.UpdateFile(file); // user accepted -> db has v2, parity still v1
    _Corrupt(file, 50);

    var outcome = this._Service().Repair(file);

    Assert.That(outcome.Result, Is.EqualTo(RepairResult.Unrepairable), "stale parity must never be applied");
  }

  [Test]
  public void Given_ModifiedFile_When_Repairing_Then_RepairIsRefused() {
    var file = this._CreateProtectedFile("edit.bin", 50_000);
    File.WriteAllBytes(file.FullName, _ExpectedContent(50_000, seed: 5)); // mtime changes -> Modified

    var outcome = this._Service().Repair(file);

    Assert.Multiple(() => {
      Assert.That(outcome.Result, Is.EqualTo(RepairResult.ModifiedNotRepaired));
      Assert.That(File.ReadAllBytes(file.FullName), Is.EqualTo(_ExpectedContent(50_000, seed: 5)), "an intentional edit must survive untouched");
    });
  }

  [Test]
  public void Given_DeletedFile_WithoutBackup_When_Repairing_Then_Unrepairable() {
    var file = this._CreateProtectedFile("gone.bin", 10_000);
    file.Delete();

    var outcome = this._Service().Repair(file);

    Assert.That(outcome.Result, Is.EqualTo(RepairResult.Unrepairable), "parity below 100% cannot recreate a whole file");
  }

  [Test]
  public void Given_DeletedFile_WithGoodBackup_When_Repairing_Then_RestoredFromBackup() {
    var file = this._CreateProtectedFile("gonem.bin", 10_000);
    this._Backup().RunBackup();
    file.Delete();

    var outcome = this._Service(withBackup: true).Repair(file);

    Assert.Multiple(() => {
      Assert.That(outcome.Result, Is.EqualTo(RepairResult.RepairedFromBackup));
      Assert.That(File.ReadAllBytes(file.FullName), Is.EqualTo(_ExpectedContent(10_000)));
    });
  }

  [Test]
  public void Given_RottenBackupCopy_When_Repairing_Then_BackupIsRefused() {
    var file = this._CreateProtectedFile("badbackup.bin", 5 * _SHARD);
    var backup = this._Backup();
    backup.RunBackup();

    // the snapshot copy itself rots
    var snapshotFile = new FileInfo(Path.Combine(backup.LatestSnapshot()!.FullName, "badbackup.bin"));
    _Corrupt(snapshotFile, 100);

    // too much damage for parity, so only the (bad) backup could help
    _Corrupt(file, 0 * _SHARD + 5, 1 * _SHARD + 5, 2 * _SHARD + 5, 3 * _SHARD + 5, 4 * _SHARD + 5);

    var outcome = this._Service(withBackup: true).Repair(file);

    Assert.That(outcome.Result, Is.EqualTo(RepairResult.Unrepairable), "a mismatching backup copy must never be restored");
  }

  [Test]
  public void Given_UntrackedFile_When_Repairing_Then_NotNeeded() {
    var file = new FileInfo(Path.Combine(this._root.FullName, "untracked.bin"));
    File.WriteAllBytes(file.FullName, new byte[10]);

    Assert.That(this._Service().Repair(file).Result, Is.EqualTo(RepairResult.NotNeeded));
  }

  [Test]
  public void Given_OkFileWithStaleParity_When_Repairing_Then_ParityIsRebuilt() {
    var file = this._CreateProtectedFile("rebind.bin", 20_000);
    File.WriteAllBytes(file.FullName, _ExpectedContent(20_000, seed: 3));
    this._checker.UpdateFile(file); // db follows the edit, parity is now stale

    var outcome = this._Service().Repair(file);

    this._checker.TryGetEntry(file, out var entry);
    Assert.Multiple(() => {
      Assert.That(outcome.Result, Is.EqualTo(RepairResult.ParityRebuilt));
      Assert.That(this._parityStore.IsParityCurrent(file, entry), Is.True);
    });
  }

  [Test]
  public void Given_EmptyFileGrownByRot_When_Repairing_Then_EmptyFileIsRestored() {
    var file = this._CreateProtectedFile("empty.bin", 0);
    file.Refresh();
    var mtime = file.LastWriteTimeUtc;
    File.WriteAllBytes(file.FullName, [1, 2, 3]);
    File.SetLastWriteTimeUtc(file.FullName, mtime); // size changed but mtime not -> still BitRot

    var outcome = this._Service().Repair(file);

    file.Refresh();
    Assert.Multiple(() => {
      Assert.That(outcome.Result, Is.EqualTo(RepairResult.Repaired));
      Assert.That(file.Length, Is.Zero);
    });
  }

  [Test]
  public void Given_TruncatedFile_WithUnchangedMtime_When_Repairing_Then_FileIsRestored() {
    var file = this._CreateProtectedFile("trunc.bin", 200_000);
    file.Refresh();
    var mtime = file.LastWriteTimeUtc;
    using (var stream = file.Open(FileMode.Open, FileAccess.ReadWrite))
      stream.SetLength(150_000); // lost tail - within one shard's worth of damage? (~49KB inside shard 2 + shard 3 gone)

    File.SetLastWriteTimeUtc(file.FullName, mtime);

    var outcome = this._Service().Repair(file);

    file.Refresh();
    Assert.Multiple(() => {
      Assert.That(outcome.Result, Is.EqualTo(RepairResult.Repaired));
      Assert.That(file.Length, Is.EqualTo(200_000));
      Assert.That(File.ReadAllBytes(file.FullName), Is.EqualTo(_ExpectedContent(200_000)));
    });
  }

}
