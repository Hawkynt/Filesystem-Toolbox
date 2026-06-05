using System.Security.Cryptography;
using Filesystem_Toolbox.Core.Redundancy;

namespace Filesystem_Toolbox.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class ParityFileTests {

  private DirectoryInfo _testDirectory = null!;

  [SetUp]
  public void SetUp() {
    this._testDirectory = new(Path.Combine(Path.GetTempPath(), $"FstParityTest_{Guid.NewGuid()}"));
    this._testDirectory.Create();
  }

  [TearDown]
  public void TearDown() {
    if (this._testDirectory.Exists)
      this._testDirectory.Delete(true);
  }

  private FileInfo _CreateSourceFile(string name, int length, int seed = 1) {
    var data = new byte[length];
    new Random(seed).NextBytes(data);
    var file = new FileInfo(Path.Combine(this._testDirectory.FullName, name));
    File.WriteAllBytes(file.FullName, data);
    return file;
  }

  private FileInfo _ParityFor(FileInfo source) => new(source.FullName + ".par");

  private static byte[] _WriteParity(FileInfo source, FileInfo destination, int percent = 25)
    => new ParityFileWriter(ParityGeometry.FromRedundancyPercent(percent)).Write(source, destination);

  // small geometry for fast tests would be nicer, but the defaults must work too - use a
  // tiny custom geometry where stripe boundaries matter and defaults where realism matters
  private static byte[] _WriteTinyParity(FileInfo source, FileInfo destination, int shardSize = 16, int k = 4, int m = 2)
    => new ParityFileWriter(new ParityGeometry(shardSize, k, m)).Write(source, destination);

  [Test]
  public void Given_SourceFile_When_WritingParity_Then_HeaderRoundTripsAndHashIsBound() {
    var source = this._CreateSourceFile("data.bin", 200_000);
    var parityFile = this._ParityFor(source);

    var hash = _WriteParity(source, parityFile);

    using var reader = ParityFileReader.Open(parityFile);
    Assert.Multiple(() => {
      Assert.That(reader.Header.OriginalLength, Is.EqualTo(200_000));
      Assert.That(reader.Header.DataShards, Is.EqualTo(16));
      Assert.That(reader.Header.ParityShards, Is.EqualTo(4));
      Assert.That(reader.Header.ShardSize, Is.EqualTo(64 * 1024));
      Assert.That(reader.Header.StripeCount, Is.EqualTo(1));
      Assert.That(reader.Header.OriginalSha512, Is.EqualTo(hash));
      Assert.That(hash, Is.EqualTo(SHA512.HashData(File.ReadAllBytes(source.FullName))), "writer-computed hash equals real content hash");
      Assert.That(reader.VerifyPayloadCrc(), Is.True);
    });
  }

  [Test]
  public void Given_EmptySourceFile_When_WritingParity_Then_FileIsExactly108BytesAndValid() {
    var source = this._CreateSourceFile("empty.bin", 0);
    var parityFile = this._ParityFor(source);

    _WriteParity(source, parityFile);

    parityFile.Refresh();
    using var reader = ParityFileReader.Open(parityFile);
    Assert.Multiple(() => {
      Assert.That(parityFile.Length, Is.EqualTo(108));
      Assert.That(reader.Header.StripeCount, Is.Zero);
      Assert.That(reader.Header.OriginalLength, Is.Zero);
      Assert.That(reader.VerifyPayloadCrc(), Is.True);
    });
  }

  [TestCase(1)]
  [TestCase(15, Description = "less than one tiny shard")]
  [TestCase(16, Description = "exactly one shard")]
  [TestCase(64, Description = "exactly one stripe")]
  [TestCase(65, Description = "one stripe plus one byte")]
  public void Given_BoundarySizedFiles_When_WritingParity_Then_ReaderAcceptsAndCrcsHold(int length) {
    var source = this._CreateSourceFile($"b{length}.bin", length);
    var parityFile = this._ParityFor(source);

    _WriteTinyParity(source, parityFile);

    using var reader = ParityFileReader.Open(parityFile);
    var expectedStripes = (length + 63) / 64;
    Assert.Multiple(() => {
      Assert.That(reader.Header.StripeCount, Is.EqualTo(expectedStripes));
      Assert.That(reader.VerifyPayloadCrc(), Is.True);
    });
  }

  [Test]
  public void Given_ParityFile_When_ReadingParityShards_Then_TheyMatchTheirRecordedCrcs() {
    var source = this._CreateSourceFile("data.bin", 1000);
    var parityFile = this._ParityFor(source);
    _WriteTinyParity(source, parityFile);

    using var reader = ParityFileReader.Open(parityFile);
    var buffer = new byte[reader.Header.ShardSize];
    for (long stripe = 0; stripe < reader.Header.StripeCount; ++stripe)
      for (var parityIndex = 0; parityIndex < reader.Header.ParityShards; ++parityIndex) {
        reader.ReadParityShard(stripe, parityIndex, buffer);
        var recorded = reader.GetShardCrc(stripe, reader.Header.DataShards + parityIndex);
        Assert.That(Crc32C.Compute(buffer, 0, buffer.Length), Is.EqualTo(recorded), $"stripe {stripe}, parity {parityIndex}");
      }
  }

  [Test]
  public void Given_DataShards_When_RecomputingTheirCrcs_Then_TheyMatchTheRecordedTable() {
    var source = this._CreateSourceFile("data.bin", 150);
    var parityFile = this._ParityFor(source);
    _WriteTinyParity(source, parityFile);

    using var reader = ParityFileReader.Open(parityFile);
    var content = File.ReadAllBytes(source.FullName);
    var shardSize = reader.Header.ShardSize;
    var k = reader.Header.DataShards;
    var shard = new byte[shardSize];
    for (long stripe = 0; stripe < reader.Header.StripeCount; ++stripe)
      for (var i = 0; i < k; ++i) {
        Array.Clear(shard, 0, shardSize);
        var offset = stripe * k * shardSize + i * shardSize;
        if (offset < content.Length)
          Array.Copy(content, offset, shard, 0, Math.Min(shardSize, content.Length - offset));

        Assert.That(Crc32C.Compute(shard, 0, shardSize), Is.EqualTo(reader.GetShardCrc(stripe, i)), $"stripe {stripe}, data shard {i}");
      }
  }

  [Test]
  public void Given_CorruptedHeaderByte_When_Opening_Then_ParityFormatExceptionIsThrown() {
    var source = this._CreateSourceFile("data.bin", 100);
    var parityFile = this._ParityFor(source);
    _WriteTinyParity(source, parityFile);

    using (var stream = parityFile.Open(FileMode.Open, FileAccess.ReadWrite)) {
      stream.Position = 24; // original length field
      var b = stream.ReadByte();
      stream.Position = 24;
      stream.WriteByte((byte)(b ^ 0xFF));
    }

    Assert.That(() => ParityFileReader.Open(parityFile), Throws.InstanceOf<ParityFormatException>());
  }

  [Test]
  public void Given_TruncatedParityFile_When_Opening_Then_ParityFormatExceptionIsThrown() {
    var source = this._CreateSourceFile("data.bin", 1000);
    var parityFile = this._ParityFor(source);
    _WriteTinyParity(source, parityFile);

    using (var stream = parityFile.Open(FileMode.Open, FileAccess.ReadWrite))
      stream.SetLength(stream.Length - 10);

    Assert.That(() => ParityFileReader.Open(parityFile), Throws.InstanceOf<ParityFormatException>());
  }

  [Test]
  public void Given_WrongMagic_When_Opening_Then_ParityFormatExceptionIsThrown() {
    var bogus = new FileInfo(Path.Combine(this._testDirectory.FullName, "bogus.par"));
    File.WriteAllBytes(bogus.FullName, new byte[200]);

    Assert.That(() => ParityFileReader.Open(bogus), Throws.InstanceOf<ParityFormatException>());
  }

  [Test]
  public void Given_UnsupportedVersion_When_Opening_Then_ParityFormatExceptionIsThrown() {
    var source = this._CreateSourceFile("data.bin", 100);
    var parityFile = this._ParityFor(source);
    _WriteTinyParity(source, parityFile);

    using (var stream = parityFile.Open(FileMode.Open, FileAccess.ReadWrite)) {
      stream.Position = 8;
      stream.WriteByte(99);
    }

    Assert.That(() => ParityFileReader.Open(parityFile), Throws.InstanceOf<ParityFormatException>());
  }

  [Test]
  public void Given_FlippedPayloadByte_When_Verifying_Then_PayloadCrcFailsAndShardCrcLocatesIt() {
    var source = this._CreateSourceFile("data.bin", 1000);
    var parityFile = this._ParityFor(source);
    _WriteTinyParity(source, parityFile);

    using (var stream = parityFile.Open(FileMode.Open, FileAccess.ReadWrite)) {

      // flip a byte inside the first parity shard of stripe 0
      var payloadOffset = ParityFileFormat.GetPayloadOffset(16, 4, 2);
      stream.Position = payloadOffset + 3;
      var b = stream.ReadByte();
      stream.Position = payloadOffset + 3;
      stream.WriteByte((byte)(b ^ 0x55));
    }

    using var reader = ParityFileReader.Open(parityFile); // header is fine - opening succeeds
    var buffer = new byte[reader.Header.ShardSize];
    reader.ReadParityShard(0, 0, buffer);

    Assert.Multiple(() => {
      Assert.That(reader.VerifyPayloadCrc(), Is.False, "whole-payload CRC must fail");
      Assert.That(Crc32C.Compute(buffer, 0, buffer.Length), Is.Not.EqualTo(reader.GetShardCrc(0, reader.Header.DataShards)), "the damaged shard's CRC must mismatch");
    });
  }

  [Test]
  public void Given_ExistingParityFile_When_WritingAgain_Then_ItIsReplacedAtomically() {
    var source = this._CreateSourceFile("data.bin", 500);
    var parityFile = this._ParityFor(source);
    _WriteTinyParity(source, parityFile);

    File.WriteAllBytes(source.FullName, new byte[500]); // change content
    var newHash = _WriteTinyParity(source, parityFile);

    using var reader = ParityFileReader.Open(parityFile);
    Assert.Multiple(() => {
      Assert.That(reader.Header.OriginalSha512, Is.EqualTo(newHash));
      Assert.That(new FileInfo(parityFile.FullName + ".tmp"), Does.Not.Exist, "temp file must be cleaned up");
    });
  }

}
