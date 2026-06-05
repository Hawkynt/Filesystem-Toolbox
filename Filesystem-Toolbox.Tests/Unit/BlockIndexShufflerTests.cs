using Filesystem_Toolbox.Core.Dedup;

namespace Filesystem_Toolbox.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class BlockIndexShufflerTests {

  [Test]
  public void Given_ZeroBlocks_When_Shuffling_Then_NoIndices()
    => Assert.That(BlockIndexShuffler.Shuffle(0), Is.Empty);

  [Test]
  public void Given_OneBlock_When_Shuffling_Then_OnlyIndexZero()
    => Assert.That(BlockIndexShuffler.Shuffle(1), Is.EqualTo(new long[] { 0 }));

  [Test]
  public void Given_EvenBlockCount_When_Shuffling_Then_AlternatesFrontAndBack()
    => Assert.That(BlockIndexShuffler.Shuffle(4), Is.EqualTo(new long[] { 0, 3, 1, 2 }));

  [Test]
  public void Given_OddBlockCount_When_Shuffling_Then_MiddleComesLast()
    => Assert.That(BlockIndexShuffler.Shuffle(5), Is.EqualTo(new long[] { 0, 4, 1, 3, 2 }));

  [TestCase(2L)]
  [TestCase(7L)]
  [TestCase(100L)]
  public void Given_AnyBlockCount_When_Shuffling_Then_EveryIndexAppearsExactlyOnce(long blockCount) {
    var indices = BlockIndexShuffler.Shuffle(blockCount).ToList();

    Assert.Multiple(() => {
      Assert.That(indices, Has.Count.EqualTo(blockCount));
      Assert.That(indices.Distinct().Count(), Is.EqualTo(blockCount));
      Assert.That(indices, Has.All.InRange(0, blockCount - 1));
    });
  }

}
