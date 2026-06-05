using System.Security.Cryptography;
using Filesystem_Toolbox.Core.Services;

namespace Filesystem_Toolbox.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class MirrorServiceTests {

  private DirectoryInfo _root = null!;
  private DirectoryInfo _mirrorRoot = null!;
  private MirrorService _service = null!;

  [SetUp]
  public void SetUp() {
    this._root = new(Path.Combine(Path.GetTempPath(), $"FstMirrorTest_{Guid.NewGuid()}"));
    this._root.Create();
    this._mirrorRoot = new(Path.Combine(Path.GetTempPath(), $"FstMirrorTarget_{Guid.NewGuid()}"));
    this._mirrorRoot.Create();
    this._service = new(this._root, this._mirrorRoot);
  }

  [TearDown]
  public void TearDown() {
    foreach (var dir in new[] { this._root, this._mirrorRoot }) {
      dir.Refresh();
      if (dir.Exists)
        dir.Delete(true);
    }
  }

  private FileInfo _CreateFile(string relativePath, string content) {
    var file = new FileInfo(Path.Combine(this._root.FullName, relativePath));
    file.Directory!.Create();
    File.WriteAllText(file.FullName, content);
    return file;
  }

  [Test]
  public void Given_File_When_Syncing_Then_MirrorCopyExistsUnderSameRelativePath() {
    var file = this._CreateFile(Path.Combine("sub", "data.txt"), "mirror me");

    this._service.Sync(file);

    var mirrorFile = new FileInfo(Path.Combine(this._mirrorRoot.FullName, "sub", "data.txt"));
    Assert.Multiple(() => {
      Assert.That(mirrorFile, Does.Exist);
      Assert.That(File.ReadAllText(mirrorFile.FullName), Is.EqualTo("mirror me"));
    });
  }

  [Test]
  public void Given_GoodMirrorCopy_When_Restoring_Then_FileIsReplacedAndContentMatches() {
    var file = this._CreateFile("data.txt", "the good content");
    var hash = SHA512.HashData(File.ReadAllBytes(file.FullName));
    this._service.Sync(file);
    File.WriteAllText(file.FullName, "damaged");

    var restored = this._service.Restore(file, hash);

    Assert.Multiple(() => {
      Assert.That(restored, Is.True);
      Assert.That(File.ReadAllText(file.FullName), Is.EqualTo("the good content"));
    });
  }

  [Test]
  public void Given_NoMirrorCopy_When_Restoring_Then_FalseIsReturned() {
    var file = this._CreateFile("lonely.txt", "no copy anywhere");

    Assert.That(this._service.Restore(file, new byte[64]), Is.False);
  }

  [Test]
  public void Given_MismatchingMirrorCopy_When_Restoring_Then_RefusedAndFileUntouched() {
    var file = this._CreateFile("data.txt", "version 1");
    this._service.Sync(file);
    File.WriteAllText(file.FullName, "version 2");
    var hashOfVersion2 = SHA512.HashData(File.ReadAllBytes(file.FullName));

    var restored = this._service.Restore(file, hashOfVersion2);

    Assert.Multiple(() => {
      Assert.That(restored, Is.False, "the mirror holds version 1 - restoring it against a version-2 hash must be refused");
      Assert.That(File.ReadAllText(file.FullName), Is.EqualTo("version 2"));
    });
  }

  [Test]
  public void Given_DeletedFile_When_RestoringFromGoodMirror_Then_FileIsRecreated() {
    var file = this._CreateFile("data.txt", "save me");
    var hash = SHA512.HashData(File.ReadAllBytes(file.FullName));
    this._service.Sync(file);
    file.Delete();

    var restored = this._service.Restore(file, hash);

    Assert.Multiple(() => {
      Assert.That(restored, Is.True);
      Assert.That(File.ReadAllText(file.FullName), Is.EqualTo("save me"));
    });
  }

  [Test]
  public void Given_NullArguments_When_Constructing_Then_ArgumentNullExceptionIsThrown() {
    Assert.Multiple(() => {
      Assert.That(() => new MirrorService(null!, this._mirrorRoot), Throws.ArgumentNullException);
      Assert.That(() => new MirrorService(this._root, null!), Throws.ArgumentNullException);
    });
  }

}
