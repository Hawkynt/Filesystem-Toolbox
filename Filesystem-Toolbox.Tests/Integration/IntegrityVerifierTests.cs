using Filesystem_Toolbox.Core.Integrity;
using Filesystem_Toolbox.Core.Redundancy;

namespace Filesystem_Toolbox.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class IntegrityVerifierTests {

  private DirectoryInfo _root = null!;
  private FolderIntegrityChecker _checker = null!;

  [SetUp]
  public void SetUp() {
    this._root = new(Path.Combine(Path.GetTempPath(), $"FstVerifierTest_{Guid.NewGuid()}"));
    this._root.Create();
    this._checker = new(this._root);
  }

  [TearDown]
  public void TearDown() {
    this._checker.Dispose();
    if (!this._root.Exists)
      return;

    foreach (var file in this._root.EnumerateFiles("*", SearchOption.AllDirectories))
      file.Attributes = FileAttributes.Normal;

    this._root.Delete(true);
  }

  private FileInfo _CreateTrackedFile(string name, string content) {
    var file = new FileInfo(Path.Combine(this._root.FullName, name));
    File.WriteAllText(file.FullName, content);
    this._checker.UpdateFile(file);
    return file;
  }

  private VerificationResult _Classify(FileInfo file) {
    this._checker.TryGetEntry(file, out var entry);
    return new IntegrityVerifier(this._checker).Classify(file, this._checker.TryGetEntry(file, out var e) ? e : null);
  }

  [Test]
  public void Given_UnchangedFile_When_Classifying_Then_Ok() {
    var file = this._CreateTrackedFile("ok.txt", "stable");

    Assert.That(this._Classify(file).Status, Is.EqualTo(VerificationStatus.Ok));
  }

  [Test]
  public void Given_ContentChangedButMtimePreserved_When_Classifying_Then_BitRot() {
    var file = this._CreateTrackedFile("rot.txt", "original");
    file.Refresh();
    var mtime = file.LastWriteTimeUtc;
    File.WriteAllText(file.FullName, "corruptd"); // same length, different bytes
    File.SetLastWriteTimeUtc(file.FullName, mtime);

    Assert.That(this._Classify(file).Status, Is.EqualTo(VerificationStatus.BitRot));
  }

  [Test]
  public void Given_ContentAndMtimeChanged_When_Classifying_Then_Modified() {
    var file = this._CreateTrackedFile("edit.txt", "original");
    File.WriteAllText(file.FullName, "edited on purpose");
    File.SetLastWriteTimeUtc(file.FullName, DateTime.UtcNow.AddMinutes(5));

    Assert.That(this._Classify(file).Status, Is.EqualTo(VerificationStatus.Modified));
  }

  [Test]
  public void Given_UntrackedFile_When_Classifying_Then_New() {
    var file = new FileInfo(Path.Combine(this._root.FullName, "new.txt"));
    File.WriteAllText(file.FullName, "shiny");

    Assert.That(this._Classify(file).Status, Is.EqualTo(VerificationStatus.New));
  }

  [Test]
  public void Given_DeletedTrackedFile_When_Classifying_Then_Missing() {
    var file = this._CreateTrackedFile("gone.txt", "soon gone");
    file.Delete();

    Assert.That(this._Classify(file).Status, Is.EqualTo(VerificationStatus.Missing));
  }

  [Test]
  public void Given_LegacyEntryWithoutMtime_When_HashDiffers_Then_ConservativelyBitRot() {
    var file = this._CreateTrackedFile("legacy.txt", "original");

    // overwrite the entry with a v1-format (no mtime) value for different content
    this._checker.TryGetEntry(file, out var entry);
    File.WriteAllText(file.FullName, "different!");
    var verifier = new IntegrityVerifier(this._checker);
    var legacyEntry = new ChecksumEntry(entry.Size, null, entry.HashBase64);

    Assert.That(verifier.Classify(file, legacyEntry).Status, Is.EqualTo(VerificationStatus.BitRot));
  }

  [Test]
  public void Given_OkFileWithoutCurrentParity_When_ClassifyingWithParityStore_Then_ParityStale() {
    var file = this._CreateTrackedFile("noparity.txt", "fine but unprotected");
    var store = new ParityStore(this._root, 25);
    this._checker.TryGetEntry(file, out var entry);

    var result = new IntegrityVerifier(this._checker, store).Classify(file, entry);

    Assert.That(result.Status, Is.EqualTo(VerificationStatus.ParityStale));
  }

  [Test]
  public void Given_OkFileWithCurrentParity_When_ClassifyingWithParityStore_Then_Ok() {
    var file = this._CreateTrackedFile("protected.txt", "fine and protected");
    var store = new ParityStore(this._root, 25);
    store.BuildParity(file);
    this._checker.TryGetEntry(file, out var entry);

    var result = new IntegrityVerifier(this._checker, store).Classify(file, entry);

    Assert.That(result.Status, Is.EqualTo(VerificationStatus.Ok));
  }

  [Test]
  public void Given_TrackedAndUntrackedAndMissingFiles_When_VerifyingAll_Then_OnlyProblemsAreReported() {
    this._CreateTrackedFile("good.txt", "all fine");
    var missing = this._CreateTrackedFile("missing.txt", "soon gone");
    missing.Delete();
    File.WriteAllText(Path.Combine(this._root.FullName, "stray.txt"), "untracked");

    var results = new IntegrityVerifier(this._checker).VerifyAll().ToList();

    Assert.Multiple(() => {
      Assert.That(results, Has.Count.EqualTo(2));
      Assert.That(results.Single(r => r.Status == VerificationStatus.Missing).File.Name, Is.EqualTo("missing.txt"));
      Assert.That(results.Single(r => r.Status == VerificationStatus.New).File.Name, Is.EqualTo("stray.txt"));
    });
  }

  [Test]
  public void Given_ParityStoreFiles_When_VerifyingAll_Then_ProtectedFolderIsIgnored() {
    var file = this._CreateTrackedFile("data.txt", "protect me");
    new ParityStore(this._root, 25).BuildParity(file);

    var results = new IntegrityVerifier(this._checker).VerifyAll().ToList();

    Assert.That(results.Select(r => r.File.Name), Has.None.EqualTo("data.txt.par"), "the .fst store must never be reported");
  }

}
