using Filesystem_Toolbox.Core.Integrity;
using Filesystem_Toolbox.Core.Redundancy;
using Filesystem_Toolbox.Core.Services;

namespace Filesystem_Toolbox.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class ParityStoreTests {

  private DirectoryInfo _root = null!;
  private ParityStore _store = null!;

  [SetUp]
  public void SetUp() {
    this._root = new(Path.Combine(Path.GetTempPath(), $"FstStoreTest_{Guid.NewGuid()}"));
    this._root.Create();
    this._store = new(this._root, 25);
  }

  [TearDown]
  public void TearDown() {
    this._root.Refresh();
    if (!this._root.Exists)
      return;

    foreach (var file in this._root.EnumerateFiles("*", SearchOption.AllDirectories))
      file.Attributes = FileAttributes.Normal;

    foreach (var dir in this._root.EnumerateDirectories("*", SearchOption.AllDirectories))
      dir.Attributes = FileAttributes.Directory;

    this._root.Delete(true);
  }

  private FileInfo _CreateFile(string relativePath, string content = "some payload") {
    var file = new FileInfo(Path.Combine(this._root.FullName, relativePath));
    file.Directory!.Create();
    File.WriteAllText(file.FullName, content);
    return file;
  }

  [Test]
  public void Given_NestedFile_When_MappingToParityFile_Then_RelativePathIsMirroredUnderTheStore() {
    var file = this._CreateFile(Path.Combine("a", "b", "data.txt"));

    var parityFile = this._store.GetParityFile(file);

    Assert.That(parityFile.FullName, Is.EqualTo(Path.Combine(this._root.FullName, ".fst", "parity", "a", "b", "data.txt.par")));
  }

  [Test]
  public void Given_File_When_BuildingParity_Then_ParityExistsAndIsCurrent() {
    var file = this._CreateFile("data.txt");

    this._store.BuildParity(file);

    var entry = ChecksumEntry.FromFile(file);
    Assert.Multiple(() => {
      Assert.That(this._store.HasParity(file), Is.True);
      Assert.That(this._store.IsParityCurrent(file, entry), Is.True);
    });
  }

  [Test]
  public void Given_EditedFile_When_CheckingParityCurrency_Then_ItIsStale() {
    var file = this._CreateFile("data.txt", "version 1");
    this._store.BuildParity(file);
    File.WriteAllText(file.FullName, "version 2");

    Assert.That(this._store.IsParityCurrent(file, ChecksumEntry.FromFile(file)), Is.False);
  }

  [Test]
  public void Given_BuiltParity_When_Deleting_Then_ParityAndEmptyDirectoriesAreGone() {
    var file = this._CreateFile(Path.Combine("deep", "nested", "data.txt"));
    this._store.BuildParity(file);

    this._store.DeleteParity(file);

    Assert.Multiple(() => {
      Assert.That(this._store.HasParity(file), Is.False);
      Assert.That(new DirectoryInfo(Path.Combine(this._store.ParityRoot.FullName, "deep")), Does.Not.Exist, "empty store directories are pruned");
    });
  }

  [Test]
  public void Given_BuiltParity_When_MovingAlongARename_Then_ParityFollowsAndStaysCurrent() {
    var file = this._CreateFile("old.txt");
    this._store.BuildParity(file);
    var entry = ChecksumEntry.FromFile(file);
    var renamed = new FileInfo(Path.Combine(this._root.FullName, "new.txt"));
    file.MoveTo(renamed.FullName);

    this._store.MoveParity(new FileInfo(Path.Combine(this._root.FullName, "old.txt")), renamed);

    Assert.Multiple(() => {
      Assert.That(this._store.HasParity(renamed), Is.True);
      Assert.That(this._store.IsParityCurrent(renamed, entry), Is.True);
    });
  }

  [Test]
  public void Given_MissingParity_When_CheckingCurrency_Then_False() {
    var file = this._CreateFile("data.txt");

    Assert.That(this._store.IsParityCurrent(file, ChecksumEntry.FromFile(file)), Is.False);
  }

  [Test]
  public void Given_CheckerWithMaintenanceQueue_When_FileIsUpdated_Then_ParityIsBuiltAutomatically() {
    using var checker = new FolderIntegrityChecker(this._root);
    using var queue = new ParityMaintenanceQueue(checker, this._store);
    var file = this._CreateFile("auto.txt", "watch me get protected");

    checker.UpdateFile(file); // raises EntryUpdated -> queue builds parity in the background

    Assert.That(
      () => this._store.HasParity(file),
      Is.True.After(10_000, 50),
      "the maintenance queue should build parity shortly after the database entry appears"
    );
  }

  [Test]
  public void Given_CheckerWithMaintenanceQueue_When_FileIsRemoved_Then_ParityIsDeletedAutomatically() {
    using var checker = new FolderIntegrityChecker(this._root);
    using var queue = new ParityMaintenanceQueue(checker, this._store);
    var file = this._CreateFile("temp.txt");
    checker.UpdateFile(file);
    Assert.That(() => this._store.HasParity(file), Is.True.After(10_000, 50));

    file.Delete();
    checker.UpdateFile(file); // removal path raises EntryRemoved

    Assert.That(() => this._store.HasParity(file), Is.False.After(10_000, 50));
  }

}
