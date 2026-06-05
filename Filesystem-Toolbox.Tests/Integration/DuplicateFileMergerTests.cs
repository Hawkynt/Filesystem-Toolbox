using Filesystem_Toolbox.Core.Dedup;

namespace Filesystem_Toolbox.Tests.Integration;

[TestFixture]
[Category("Integration")]
[Platform("Win")]
public class DuplicateFileMergerTests {

  private DirectoryInfo _root = null!;

  [SetUp]
  public void SetUp() {
    this._root = new(Path.Combine(Path.GetTempPath(), $"FstDedupTest_{Guid.NewGuid()}"));
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

  private FileInfo _CreateFile(string relativePath, byte[] content) {
    var file = new FileInfo(Path.Combine(this._root.FullName, relativePath));
    file.Directory!.Create();
    File.WriteAllBytes(file.FullName, content);
    return file;
  }

  private static byte[] _Payload(int length, int seed = 21) {
    var result = new byte[length];
    new Random(seed).NextBytes(result);
    return result;
  }

  private static DedupReport _Run(DirectoryInfo root, Action<DedupOptions>? configure = null) {
    var options = new DedupOptions { MaximumCrawlerThreads = 2 };
    configure?.Invoke(options);
    return DuplicateFileMerger.ProcessFolders([root], options);
  }

  [Test]
  public void Given_TwoIdenticalFiles_When_Merging_Then_TheyBecomeHardLinksWithReadOnlyAttribute() {
    var payload = _Payload(100_000);
    var a = this._CreateFile("a.bin", payload);
    var b = this._CreateFile(Path.Combine("sub", "b.bin"), payload);

    var report = _Run(this._root);

    a.Refresh();
    b.Refresh();
    Assert.Multiple(() => {
      Assert.That(report.HardLinksCreated, Is.EqualTo(1));
      Assert.That(File.ReadAllBytes(b.FullName), Is.EqualTo(payload), "content must survive the merge");
      Assert.That(a.GetHardLinkTargets().Concat(b.GetHardLinkTargets()), Is.Not.Empty, "the files must reference each other");
      Assert.That(
        (a.Attributes | b.Attributes) & FileAttributes.ReadOnly,
        Is.EqualTo(FileAttributes.ReadOnly),
        "the merged link gets read-only by default (NTFS hard links are not copy-on-write)"
      );
    });
  }

  [Test]
  public void Given_TwoDifferentFilesOfSameSize_When_Merging_Then_NothingIsLinked() {
    this._CreateFile("a.bin", _Payload(50_000, seed: 1));
    this._CreateFile("b.bin", _Payload(50_000, seed: 2));

    var report = _Run(this._root);

    Assert.That(report.HardLinksCreated, Is.Zero);
  }

  [Test]
  public void Given_IdenticalFiles_When_DryRunning_Then_DuplicatesAreReportedButNothingChanges() {
    var payload = _Payload(10_000);
    var a = this._CreateFile("a.bin", payload);
    var b = this._CreateFile("b.bin", payload);

    var report = _Run(this._root, o => o.ShowInfoOnly = true);

    a.Refresh();
    Assert.Multiple(() => {
      Assert.That(report.HardLinksCreated, Is.EqualTo(1), "the duplicate is counted in dry-run mode");
      Assert.That(a.GetHardLinkTargets(), Is.Empty, "no link may actually be created");
      Assert.That(a.Attributes & FileAttributes.ReadOnly, Is.EqualTo((FileAttributes)0));
    });
  }

  [Test]
  public void Given_FilteredDirectory_When_Merging_Then_ItsFilesAreNeverTouched() {
    var payload = _Payload(10_000);
    this._CreateFile("a.bin", payload);
    var inProtected = this._CreateFile(Path.Combine(".fst", "b.bin"), payload);

    var report = _Run(this._root, o => o.DirectoryFilter = d => d.Name != ".fst");

    inProtected.Refresh();
    Assert.Multiple(() => {
      Assert.That(report.HardLinksCreated, Is.Zero);
      Assert.That(inProtected.GetHardLinkTargets(), Is.Empty);
    });
  }

  [Test]
  public void Given_FilesBelowMinimumSize_When_Merging_Then_TheyAreSkipped() {
    var payload = _Payload(100);
    this._CreateFile("a.bin", payload);
    this._CreateFile("b.bin", payload);

    var report = _Run(this._root, o => o.MinimumFileSizeInBytes = 1000);

    Assert.That(report.HardLinksCreated, Is.Zero);
  }

  [Test]
  public void Given_ThreeIdenticalFiles_When_Merging_Then_AllShareTheSameData() {
    var payload = _Payload(20_000);
    var files = new[] {
      this._CreateFile("a.bin", payload),
      this._CreateFile("b.bin", payload),
      this._CreateFile("c.bin", payload),
    };

    var report = _Run(this._root);

    Assert.Multiple(() => {
      Assert.That(report.HardLinksCreated, Is.GreaterThanOrEqualTo(2));
      foreach (var file in files) {
        file.Refresh();
        Assert.That(File.ReadAllBytes(file.FullName), Is.EqualTo(payload), file.Name);
      }
    });
  }

}
