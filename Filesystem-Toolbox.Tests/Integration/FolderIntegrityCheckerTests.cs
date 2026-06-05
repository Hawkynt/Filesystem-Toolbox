using Filesystem_Toolbox.Core.Integrity;

namespace Filesystem_Toolbox.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class FolderIntegrityCheckerTests {

  private DirectoryInfo _root = null!;

  [SetUp]
  public void SetUp() {
    this._root = new(Path.Combine(Path.GetTempPath(), $"FstIntegrityTest_{Guid.NewGuid()}"));
    this._root.Create();
  }

  [TearDown]
  public void TearDown() {
    if (!this._root.Exists)
      return;

    foreach (var file in this._root.EnumerateFiles("*", SearchOption.AllDirectories))
      file.Attributes = FileAttributes.Normal;

    this._root.Delete(true);
  }

  private FileInfo _CreateFile(string relativePath, string content) {
    var file = new FileInfo(Path.Combine(this._root.FullName, relativePath));
    file.Directory!.Create();
    File.WriteAllText(file.FullName, content);
    return file;
  }

  private static List<(FileInfo File, string Old, string Current)> _Verify(FolderIntegrityChecker checker) {
    var failures = new List<(FileInfo, string, string)>();
    checker.VerifyIntegrity((f, o, n) => failures.Add((f, o, n)));
    return failures;
  }

  [Test]
  public void Given_FolderWithFiles_When_RebuildingDatabase_Then_AllFilesAreRecorded() {
    this._CreateFile("a.txt", "alpha");
    this._CreateFile(Path.Combine("sub", "b.txt"), "beta");

    using var checker = new FolderIntegrityChecker(this._root);
    checker.RebuildDatabase();

    var known = checker.KnownFiles.Select(f => f.Name).OrderBy(n => n).ToArray();
    Assert.That(known, Is.EqualTo(new[] { "a.txt", "b.txt" }));
  }

  [Test]
  public void Given_RebuiltDatabase_When_SavingAndLoadingWithNewInstance_Then_EntriesRoundTrip() {
    this._CreateFile("a.txt", "alpha");

    using (var checker = new FolderIntegrityChecker(this._root)) {
      checker.RebuildDatabase();
      checker.SaveDatabase();
    }

    using var reloaded = FolderIntegrityChecker.Create(this._root);
    Assert.That(reloaded.KnownFiles.Select(f => f.Name), Is.EqualTo(new[] { "a.txt" }));
  }

  [Test]
  public void Given_UnchangedFiles_When_Verifying_Then_NothingIsReported() {
    this._CreateFile("a.txt", "alpha");

    using var checker = new FolderIntegrityChecker(this._root);
    checker.RebuildDatabase();

    Assert.That(_Verify(checker), Is.Empty);
  }

  [Test]
  public void Given_ModifiedFileContent_When_Verifying_Then_ChecksumFailureIsReported() {
    var file = this._CreateFile("a.txt", "alpha");

    using var checker = new FolderIntegrityChecker(this._root);
    checker.RebuildDatabase();
    File.WriteAllText(file.FullName, "ALPHA!");

    var failures = _Verify(checker);
    Assert.Multiple(() => {
      Assert.That(failures, Has.Count.EqualTo(1));
      Assert.That(failures[0].File.Name, Is.EqualTo("a.txt"));
      Assert.That(failures[0].Old, Is.Not.Null.And.Not.EqualTo(failures[0].Current));
    });
  }

  [Test]
  public void Given_UntrackedNewFile_When_Verifying_Then_ItIsReportedWithoutOldChecksum() {
    this._CreateFile("a.txt", "alpha");

    using var checker = new FolderIntegrityChecker(this._root);
    checker.RebuildDatabase();
    this._CreateFile("new.txt", "shiny");

    var failures = _Verify(checker);
    Assert.Multiple(() => {
      Assert.That(failures, Has.Count.EqualTo(1));
      Assert.That(failures[0].File.Name, Is.EqualTo("new.txt"));
      Assert.That(failures[0].Old, Is.Null);
    });
  }

  [Test]
  public void Given_ModifiedFile_When_AcceptingViaUpdateFile_Then_VerificationPassesAgain() {
    var file = this._CreateFile("a.txt", "alpha");

    using var checker = new FolderIntegrityChecker(this._root);
    checker.RebuildDatabase();
    File.WriteAllText(file.FullName, "ALPHA!");
    checker.UpdateFile(file);

    Assert.That(_Verify(checker), Is.Empty);
  }

  [Test]
  public void Given_FileOutsideRoot_When_UpdatingFile_Then_ArgumentExceptionIsThrown() {
    using var checker = new FolderIntegrityChecker(this._root);
    var outsider = new FileInfo(Path.Combine(Path.GetTempPath(), $"outsider_{Guid.NewGuid()}.txt"));

    Assert.That(() => checker.UpdateFile(outsider), Throws.ArgumentException);
  }

  [Test]
  public void Given_SavedDatabase_When_Rebuilding_Then_DatabaseFileItselfIsNotTracked() {
    this._CreateFile("a.txt", "alpha");

    using var checker = new FolderIntegrityChecker(this._root);
    checker.RebuildDatabase();
    checker.SaveDatabase();
    checker.RebuildDatabase();

    Assert.That(checker.KnownFiles.Select(f => f.Name), Has.None.EqualTo("checksum.db"));
  }

  [Test]
  public void Given_EmptyFolder_When_RebuildingAndVerifying_Then_NothingIsReported() {
    using var checker = new FolderIntegrityChecker(this._root);
    checker.RebuildDatabase();

    Assert.Multiple(() => {
      Assert.That(checker.KnownFiles, Is.Empty);
      Assert.That(_Verify(checker), Is.Empty);
    });
  }

}
