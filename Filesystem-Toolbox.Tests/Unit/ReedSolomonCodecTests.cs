using Filesystem_Toolbox.Core.Redundancy;

namespace Filesystem_Toolbox.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class ReedSolomonCodecTests {

  private const int _SHARD_LENGTH = 128;

  private static byte[][] _CreateShards(int count, int seed) {
    var random = new Random(seed);
    var result = new byte[count][];
    for (var i = 0; i < count; ++i) {
      result[i] = new byte[_SHARD_LENGTH];
      random.NextBytes(result[i]);
    }

    return result;
  }

  private static (byte[][] shards, byte[][] originalData) _EncodeAll(ReedSolomonCodec codec, int seed) {
    var data = _CreateShards(codec.DataShards, seed);
    var parity = new byte[codec.ParityShards][];
    for (var i = 0; i < codec.ParityShards; ++i)
      parity[i] = new byte[_SHARD_LENGTH];

    codec.Encode(data, parity, _SHARD_LENGTH);

    var shards = new byte[codec.TotalShards][];
    var originals = new byte[codec.DataShards][];
    for (var i = 0; i < codec.DataShards; ++i) {
      shards[i] = (byte[])data[i].Clone();
      originals[i] = (byte[])data[i].Clone();
    }

    for (var i = 0; i < codec.ParityShards; ++i)
      shards[codec.DataShards + i] = (byte[])parity[i].Clone();

    return (shards, originals);
  }

  [TestCase(1, 1)]
  [TestCase(2, 3)]
  [TestCase(16, 1)]
  [TestCase(16, 4)]
  [TestCase(16, 8)]
  [TestCase(16, 16)]
  [TestCase(250, 5)]
  public void Given_EncodedShards_When_DecodingWithoutErasures_Then_DataIsUnchanged(int k, int m) {
    var codec = new ReedSolomonCodec(k, m);
    var (shards, originals) = _EncodeAll(codec, 42);
    var present = Enumerable.Repeat(true, codec.TotalShards).ToArray();

    Assert.That(codec.DecodeErasures(shards, present, _SHARD_LENGTH), Is.True);
    for (var i = 0; i < k; ++i)
      Assert.That(shards[i], Is.EqualTo(originals[i]), $"data shard {i}");
  }

  [Test]
  public void Given_EncodedShards_When_ErasingAnySingleShard_Then_ItIsReconstructed() {
    var codec = new ReedSolomonCodec(16, 4);
    for (var erased = 0; erased < codec.TotalShards; ++erased) {
      var (shards, originals) = _EncodeAll(codec, erased);
      var expectedParity = new byte[codec.ParityShards][];
      for (var i = 0; i < codec.ParityShards; ++i)
        expectedParity[i] = (byte[])shards[16 + i].Clone();

      var present = Enumerable.Repeat(true, codec.TotalShards).ToArray();
      new Random(erased).NextBytes(shards[erased]); // garbage where the shard used to be
      present[erased] = false;

      Assert.That(codec.DecodeErasures(shards, present, _SHARD_LENGTH), Is.True, $"erased shard {erased}");
      for (var i = 0; i < 16; ++i)
        Assert.That(shards[i], Is.EqualTo(originals[i]), $"data shard {i} after erasing {erased}");
      for (var i = 0; i < codec.ParityShards; ++i)
        Assert.That(shards[16 + i], Is.EqualTo(expectedParity[i]), $"parity shard {i} after erasing {erased}");
    }
  }

  [Test]
  public void Given_EncodedShards_When_ErasingExactlyMShards_Then_AllAreReconstructed() {
    var codec = new ReedSolomonCodec(16, 4);

    // equivalence classes: all-data, all-parity, mixed erasure patterns of size m
    int[][] patterns = [[0, 1, 2, 3], [16, 17, 18, 19], [0, 5, 17, 19], [15, 16, 7, 18]];
    foreach (var pattern in patterns) {
      var (shards, originals) = _EncodeAll(codec, pattern[0] * 31 + pattern[1]);
      var present = Enumerable.Repeat(true, codec.TotalShards).ToArray();
      foreach (var erased in pattern) {
        Array.Clear(shards[erased], 0, _SHARD_LENGTH);
        present[erased] = false;
      }

      Assert.That(codec.DecodeErasures(shards, present, _SHARD_LENGTH), Is.True, $"pattern [{string.Join(",", pattern)}]");
      for (var i = 0; i < 16; ++i)
        Assert.That(shards[i], Is.EqualTo(originals[i]), $"data shard {i}, pattern [{string.Join(",", pattern)}]");
    }
  }

  [Test]
  public void Given_EncodedShards_When_ErasingMPlusOneShards_Then_DecodeReturnsFalseWithoutThrowing() {
    var codec = new ReedSolomonCodec(16, 4);
    var (shards, _) = _EncodeAll(codec, 99);
    var present = Enumerable.Repeat(true, codec.TotalShards).ToArray();
    foreach (var erased in new[] { 0, 1, 2, 3, 4 })
      present[erased] = false;

    Assert.That(codec.DecodeErasures(shards, present, _SHARD_LENGTH), Is.False);
  }

  [TestCase(0, 1)]
  [TestCase(1, 0)]
  [TestCase(-1, 4)]
  [TestCase(200, 56)]
  public void Given_InvalidShardCounts_When_Constructing_Then_ArgumentOutOfRangeExceptionIsThrown(int k, int m)
    => Assert.That(() => new ReedSolomonCodec(k, m), Throws.InstanceOf<ArgumentOutOfRangeException>());

  [Test]
  public void Given_MaximumShardConfiguration_When_Constructing_Then_ItSucceeds()
    => Assert.That(() => new ReedSolomonCodec(127, 128), Throws.Nothing);

  [Test]
  public void Given_WrongShardArrayCounts_When_EncodingOrDecoding_Then_ArgumentExceptionIsThrown() {
    var codec = new ReedSolomonCodec(4, 2);
    var tooFew = _CreateShards(3, 1);
    var parity = _CreateShards(2, 2);

    Assert.Multiple(() => {
      Assert.That(() => codec.Encode(tooFew, parity, _SHARD_LENGTH), Throws.ArgumentException);
      Assert.That(() => codec.DecodeErasures(tooFew, new bool[6], _SHARD_LENGTH), Throws.ArgumentException);
      Assert.That(() => codec.Encode(null!, parity, _SHARD_LENGTH), Throws.ArgumentNullException);
    });
  }

  [Test]
  public void Given_SingularMatrix_When_Inverting_Then_InvalidOperationExceptionIsThrown() {
    byte[][] singular = [[1, 2], [1, 2]];

    Assert.That(() => ReedSolomonCodec.InvertMatrix(singular), Throws.InvalidOperationException);
  }

  [Test]
  public void Given_RandomInvertibleMatrix_When_InvertedTwice_Then_OriginalIsRecovered() {
    byte[][] matrix = [[1, 2, 3], [4, 5, 6], [7, 8, 10]];

    var inverse = ReedSolomonCodec.InvertMatrix(matrix);
    var doubleInverse = ReedSolomonCodec.InvertMatrix(inverse);

    Assert.That(doubleInverse, Is.EqualTo(matrix));
  }

}
