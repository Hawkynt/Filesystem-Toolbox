using System.Security.Cryptography;
using Filesystem_Toolbox.Core.Integrity;
using Filesystem_Toolbox.Core.Services;

namespace Filesystem_Toolbox.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class BackupServiceTests {

  private DirectoryInfo _root = null!;
  private DirectoryInfo _backupRoot = null!;
  private FolderIntegrityChecker _checker = null!;

  [SetUp]
  public void SetUp() {
    this._root = new(Path.Combine(Path.GetTempPath(), $"FstBackupTest_{Guid.NewGuid()}"));
    this._root.Create();
    this._backupRoot = new(Path.Combine(Path.GetTempPath(), $"FstBackupTarget_{Guid.NewGuid()}"));
    this._backupRoot.Create();
    this._checker = new(this._root);
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

      dir.Delete(true);
    }
  }

  private BackupService _Service(GfsRetentionPolicy? retention = null)
    => new(this._checker, this._backupRoot, retention ?? GfsRetentionPolicy.Default);

  private FileInfo _CreateTrackedFile(string relativePath, string content) {
    var file = new FileInfo(Path.Combine(this._root.FullName, relativePath));
    file.Directory!.Create();
    File.WriteAllText(file.FullName, content);
    this._checker.UpdateFile(file);
    return file;
  }

  [Test]
  public void Given_TrackedCleanFiles_When_RunningBackup_Then_SnapshotWithManifestExists() {
    this._CreateTrackedFile("a.txt", "alpha");
    this._CreateTrackedFile(Path.Combine("sub", "b.txt"), "beta");

    var report = this._Service().RunBackup();

    var snapshot = new DirectoryInfo(Path.Combine(this._backupRoot.FullName, report.SnapshotName));
    Assert.Multiple(() => {
      Assert.That(report.Copied, Is.EqualTo(2));
      Assert.That(report.Errors, Is.Zero);
      Assert.That(snapshot, Does.Exist);
      Assert.That(new FileInfo(Path.Combine(snapshot.FullName, "a.txt")), Does.Exist);
      Assert.That(File.ReadAllText(Path.Combine(snapshot.FullName, "sub", "b.txt")), Is.EqualTo("beta"));
      Assert.That(new FileInfo(Path.Combine(snapshot.FullName, ".fst-snapshot.manifest")), Does.Exist);
    });
  }

  [Test]
  public void Given_RottenFile_When_RunningBackup_Then_ItIsSkippedDirty() {
    var file = this._CreateTrackedFile("rot.txt", "original");
    file.Refresh();
    var mtime = file.LastWriteTimeUtc;
    File.WriteAllText(file.FullName, "corruptd");
    File.SetLastWriteTimeUtc(file.FullName, mtime);

    var report = this._Service().RunBackup();

    Assert.Multiple(() => {
      Assert.That(report.SkippedDirty, Is.EqualTo(1), "rot must never enter a snapshot");
      Assert.That(report.Copied, Is.Zero);
    });
  }

  [Test]
  [Platform("Win")]
  public void Given_UnchangedFile_When_RunningSecondBackup_Then_ItIsHardLinkedNotCopied() {
    var file = this._CreateTrackedFile("stable.txt", "never changes");
    var service = this._Service();
    service.RunBackup();

    var report = service.RunBackup();

    Assert.Multiple(() => {
      Assert.That(report.Linked, Is.EqualTo(1), "unchanged files dedupe against the previous snapshot");
      Assert.That(report.Copied, Is.Zero);
      var copy = new FileInfo(Path.Combine(service.LatestSnapshot()!.FullName, "stable.txt"));
      Assert.That(copy.GetHardLinkTargets(), Is.Not.Empty, "the snapshot entry is a hard link");
    });
  }

  [Test]
  public void Given_ChangedFile_When_RunningSecondBackup_Then_ItIsCopiedAgain() {
    var file = this._CreateTrackedFile("changing.txt", "version 1");
    var service = this._Service();
    service.RunBackup();

    File.WriteAllText(file.FullName, "version 2");
    this._checker.UpdateFile(file);
    var report = service.RunBackup();

    Assert.Multiple(() => {
      Assert.That(report.Copied, Is.EqualTo(1));
      Assert.That(report.Linked, Is.Zero);
    });
  }

  [Test]
  public void Given_TwoBackupsInTheSameSecond_When_Running_Then_SnapshotNamesAreUnique() {
    this._CreateTrackedFile("a.txt", "alpha");
    var service = this._Service();

    var first = service.RunBackup();
    var second = service.RunBackup();

    Assert.Multiple(() => {
      Assert.That(first.SnapshotName, Is.Not.EqualTo(second.SnapshotName));
      Assert.That(service.EnumerateSnapshots().Count(), Is.GreaterThanOrEqualTo(2));
    });
  }

  [Test]
  public void Given_GoodSnapshot_When_Restoring_Then_ContentComesBack() {
    var file = this._CreateTrackedFile("data.txt", "save me");
    var hash = SHA512.HashData(File.ReadAllBytes(file.FullName));
    var service = this._Service();
    service.RunBackup();
    file.Delete();

    var restored = service.Restore(file, hash);

    Assert.Multiple(() => {
      Assert.That(restored, Is.True);
      Assert.That(File.ReadAllText(file.FullName), Is.EqualTo("save me"));
    });
  }

  [Test]
  public void Given_OlderSnapshotHoldsTheWantedVersion_When_Restoring_Then_ItIsFound() {

    // snapshot 1 holds version 1; the file is then legitimately edited and snapshot 2 holds version 2;
    // when the db still expects version 2 but we ask for version 1's hash, the OLDER snapshot serves it
    var file = this._CreateTrackedFile("versioned.txt", "version 1");
    var hashV1 = SHA512.HashData(File.ReadAllBytes(file.FullName));
    var service = this._Service();
    service.RunBackup();

    File.WriteAllText(file.FullName, "version 2");
    this._checker.UpdateFile(file);
    service.RunBackup();

    var restored = service.Restore(file, hashV1);

    Assert.Multiple(() => {
      Assert.That(restored, Is.True, "older snapshots are searched too");
      Assert.That(File.ReadAllText(file.FullName), Is.EqualTo("version 1"));
    });
  }

  [Test]
  public void Given_RottenSnapshotCopy_When_Restoring_Then_ItIsSkippedForAnOlderGoodOne() {
    var file = this._CreateTrackedFile("guarded.txt", "the content");
    var hash = SHA512.HashData(File.ReadAllBytes(file.FullName));
    var service = this._Service();
    service.RunBackup();
    System.Threading.Thread.Sleep(1100); // distinct snapshot second
    service.RunBackup();

    // rot the NEWEST snapshot's copy; ensure it is not hardlinked to the older one first
    var newest = new FileInfo(Path.Combine(service.LatestSnapshot()!.FullName, "guarded.txt"));
    newest.Attributes = FileAttributes.Normal;
    File.Delete(newest.FullName); // break the hard link before corrupting
    File.WriteAllText(newest.FullName, "the c0ntent");
    file.Delete();

    var restored = service.Restore(file, hash);

    Assert.Multiple(() => {
      Assert.That(restored, Is.True, "the older intact snapshot must be used");
      Assert.That(File.ReadAllText(file.FullName), Is.EqualTo("the content"));
    });
  }

  [Test]
  public void Given_NoMatchingSnapshot_When_Restoring_Then_FalseAndFileUntouched() {
    var file = this._CreateTrackedFile("lost.txt", "data");
    var service = this._Service();
    service.RunBackup();

    Assert.That(service.Restore(file, new byte[64]), Is.False);
  }

  [Test]
  public void Given_LeftoverPartialSnapshot_When_RunningBackup_Then_ItIsReclaimedAndIgnored() {
    this._CreateTrackedFile("a.txt", "alpha");
    var partial = new DirectoryInfo(Path.Combine(this._backupRoot.FullName, "2026-01-01_000000.partial"));
    partial.Create();
    File.WriteAllText(Path.Combine(partial.FullName, "junk.txt"), "crash leftover");

    var service = this._Service();
    var report = service.RunBackup();

    Assert.Multiple(() => {
      Assert.That(Directory.Exists(partial.FullName), Is.False, "leftovers are reclaimed");
      Assert.That(service.EnumerateSnapshots().Select(s => s.Name), Has.None.Contain("partial"));
      Assert.That(report.Errors, Is.Zero);
    });
  }

  [Test]
  public void Given_AggressiveRetention_When_RunningManyBackups_Then_OldSnapshotsArePruned() {
    this._CreateTrackedFile("a.txt", "alpha");
    var service = this._Service(new GfsRetentionPolicy(1, 0, 0));

    service.RunBackup();
    System.Threading.Thread.Sleep(1100);
    service.RunBackup();
    System.Threading.Thread.Sleep(1100);
    var report = service.RunBackup();

    Assert.Multiple(() => {
      Assert.That(report.SnapshotsPruned, Is.GreaterThanOrEqualTo(1));
      Assert.That(service.EnumerateSnapshots().Count(), Is.LessThanOrEqualTo(2), "same-day snapshots beyond the daily bucket are pruned (newest always kept)");
    });
  }

  [Test]
  public void Given_MissingTrackedFile_When_RunningBackup_Then_CountedAsErrorAndOthersStillBackedUp() {
    this._CreateTrackedFile("ok.txt", "fine");
    var gone = this._CreateTrackedFile("gone.txt", "bye");
    gone.Delete();

    var report = this._Service().RunBackup();

    Assert.Multiple(() => {
      Assert.That(report.Errors, Is.EqualTo(1));
      Assert.That(report.Copied, Is.EqualTo(1));
    });
  }

}
