using Filesystem_Toolbox.Core.Redundancy;

namespace Filesystem_Toolbox.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class ParityRepairCoreTests {

  private DirectoryInfo _root = null!;

  [SetUp]
  public void SetUp() {
    this._root = new(Path.Combine(Path.GetTempPath(), $"FstRepairCoreTest_{Guid.NewGuid()}"));
    this._root.Create();
  }

  [TearDown]
  public void TearDown() {
    if (this._root.Exists)
      this._root.Delete(true);
  }

  private (FileInfo file, FileInfo parity, byte[] content) _CreateProtectedFile(int length, int seed = 11) {
    var content = new byte[length];
    new Random(seed).NextBytes(content);
    var file = new FileInfo(Path.Combine(this._root.FullName, "data.bin"));
    File.WriteAllBytes(file.FullName, content);
    var parity = new FileInfo(Path.Combine(this._root.FullName, "data.bin.par"));
    new ParityFileWriter(new ParityGeometry(1024, 8, 2)).Write(file, parity);
    return (file, parity, content);
  }

  private static void _Corrupt(FileInfo file, params long[] offsets) {
    using var stream = file.Open(FileMode.Open, FileAccess.ReadWrite);
    foreach (var offset in offsets) {
      stream.Position = offset;
      var b = stream.ReadByte();
      stream.Position = offset;
      stream.WriteByte((byte)(b ^ 0xFF));
    }
  }

  [Test]
  public void Given_DamagedFile_When_RepairingWithNullExpectedHash_Then_HeaderHashIsTrusted() {

    // this is the self-healing-database case: no external entry exists, the parity header
    // itself carries the expected hash (and the header CRC proves the header)
    var (file, parity, content) = this._CreateProtectedFile(10_000);
    _Corrupt(file, 5, 2000);

    var outcome = ParityRepairCore.TryRepairFile(file, parity);

    Assert.Multiple(() => {
      Assert.That(outcome.Repaired, Is.True);
      Assert.That(File.ReadAllBytes(file.FullName), Is.EqualTo(content));
    });
  }

  [Test]
  public void Given_StaleParity_When_RepairingWithMismatchingExpectedHash_Then_Refused() {
    var (file, parity, _) = this._CreateProtectedFile(5_000);
    _Corrupt(file, 5);
    var wrongHash = new byte[64]; // pretend the database expects different content

    var outcome = ParityRepairCore.TryRepairFile(file, parity, wrongHash);

    Assert.That(outcome.Repaired, Is.False, "parity bound to other content must never be applied");
  }

  [Test]
  public void Given_DamageBeyondParity_When_Repairing_Then_FileLeftIntactAndTempCleanedUp() {
    var (file, parity, _) = this._CreateProtectedFile(8 * 1024); // 8 shards of 1 KiB, m=2
    var damaged = new long[] { 5, 1030, 2060, 3090 }; // 4 bad shards > m=2
    _Corrupt(file, damaged);
    var damagedContent = File.ReadAllBytes(file.FullName);

    var outcome = ParityRepairCore.TryRepairFile(file, parity);

    Assert.Multiple(() => {
      Assert.That(outcome.Repaired, Is.False);
      Assert.That(File.ReadAllBytes(file.FullName), Is.EqualTo(damagedContent), "a failed repair must not touch the file");
      Assert.That(new FileInfo(file.FullName + ".fst-repair"), Does.Not.Exist);
    });
  }

  [Test]
  public void Given_StructurallyBrokenParity_When_Repairing_Then_ParityFormatExceptionPropagates() {
    var (file, parity, _) = this._CreateProtectedFile(1000);
    File.WriteAllBytes(parity.FullName, new byte[50]); // garbage

    Assert.That(() => ParityRepairCore.TryRepairFile(file, parity), Throws.InstanceOf<ParityFormatException>());
  }

}
