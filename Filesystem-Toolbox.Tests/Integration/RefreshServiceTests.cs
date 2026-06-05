using Filesystem_Toolbox.Core.Integrity;
using Filesystem_Toolbox.Core.Services;

namespace Filesystem_Toolbox.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class RefreshServiceTests {

  private DirectoryInfo _root = null!;
  private FolderIntegrityChecker _checker = null!;

  [SetUp]
  public void SetUp() {
    this._root = new(Path.Combine(Path.GetTempPath(), $"FstRefreshTest_{Guid.NewGuid()}"));
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

  private FileInfo _CreateTrackedFile(string name, string content, TimeSpan age) {
    var file = new FileInfo(Path.Combine(this._root.FullName, name));
    File.WriteAllText(file.FullName, content);
    File.SetLastWriteTimeUtc(file.FullName, DateTime.UtcNow - age);
    this._checker.UpdateFile(file);
    return file;
  }

  [Test]
  public void Given_FileOlderThanInterval_When_Refreshing_Then_RewrittenWithPreservedMetadata() {
    var file = this._CreateTrackedFile("old.txt", "needs recharging", TimeSpan.FromDays(10));
    file.Refresh();
    var originalMtime = file.LastWriteTimeUtc;
    var service = new RefreshService(this._checker, TimeSpan.FromDays(5));

    var report = service.RefreshDue();

    file.Refresh();
    Assert.Multiple(() => {
      Assert.That(report.Refreshed, Is.EqualTo(1));
      Assert.That(File.ReadAllText(file.FullName), Is.EqualTo("needs recharging"));
      Assert.That(file.LastWriteTimeUtc, Is.EqualTo(originalMtime), "refresh must be invisible to integrity classification");
      Assert.That(service.GetLastRefresh(file), Is.Not.Null);
    });
  }

  [Test]
  public void Given_RecentlyWrittenFile_When_Refreshing_Then_SkippedNotDue() {
    this._CreateTrackedFile("young.txt", "fresh", TimeSpan.Zero);

    var report = new RefreshService(this._checker, TimeSpan.FromDays(5)).RefreshDue();

    Assert.Multiple(() => {
      Assert.That(report.Refreshed, Is.Zero);
      Assert.That(report.SkippedNotDue, Is.EqualTo(1));
    });
  }

  [Test]
  public void Given_JustRefreshedFile_When_RefreshingAgain_Then_SkippedNotDue() {
    this._CreateTrackedFile("once.txt", "refresh me once", TimeSpan.FromDays(10));
    var service = new RefreshService(this._checker, TimeSpan.FromDays(5));

    var first = service.RefreshDue();
    var second = service.RefreshDue();

    Assert.Multiple(() => {
      Assert.That(first.Refreshed, Is.EqualTo(1));
      Assert.That(second.Refreshed, Is.Zero);
      Assert.That(second.SkippedNotDue, Is.EqualTo(1), "the refresh timestamp must persist between runs");
    });
  }

  [Test]
  public void Given_CorruptedFile_When_Refreshing_Then_SkippedDirtyAndNotRewritten() {
    var file = this._CreateTrackedFile("dirty.txt", "original", TimeSpan.FromDays(10));
    file.Refresh();
    var mtime = file.LastWriteTimeUtc;
    File.WriteAllText(file.FullName, "corruptd");
    File.SetLastWriteTimeUtc(file.FullName, mtime);

    var report = new RefreshService(this._checker, TimeSpan.FromDays(5)).RefreshDue();

    Assert.Multiple(() => {
      Assert.That(report.SkippedDirty, Is.EqualTo(1), "rewriting corruption would make it permanent");
      Assert.That(report.Refreshed, Is.Zero);
      Assert.That(File.ReadAllText(file.FullName), Is.EqualTo("corruptd"), "the damaged file must stay untouched for repair");
    });
  }

  [Test]
  public void Given_MissingTrackedFile_When_Refreshing_Then_CountedAsError() {
    var file = this._CreateTrackedFile("gone.txt", "bye", TimeSpan.FromDays(10));
    file.Delete();

    var report = new RefreshService(this._checker, TimeSpan.FromDays(5)).RefreshDue();

    Assert.That(report.Errors, Is.EqualTo(1));
  }

  [Test]
  public void Given_InvalidInterval_When_Constructing_Then_ArgumentOutOfRangeExceptionIsThrown()
    => Assert.That(() => new RefreshService(this._checker, TimeSpan.Zero), Throws.InstanceOf<ArgumentOutOfRangeException>());

}
