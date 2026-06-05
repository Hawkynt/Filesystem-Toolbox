using Filesystem_Toolbox.Core.Dedup;

namespace Filesystem_Toolbox.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class BlockComparerTests {

  // boundary lengths around the unrolled (64-byte), 8-byte and 4-byte comparison kernels
  [TestCase(0)]
  [TestCase(1)]
  [TestCase(3)]
  [TestCase(4)]
  [TestCase(7)]
  [TestCase(8)]
  [TestCase(63)]
  [TestCase(64)]
  [TestCase(65)]
  [TestCase(1027)]
  public void Given_IdenticalBuffers_When_Comparing_Then_TheyAreEqual(int length) {
    var a = new byte[length];
    new Random(length).NextBytes(a);
    var b = (byte[])a.Clone();

    Assert.That(BlockComparer.IsEqual(a, length, b, length), Is.True);
  }

  [TestCase(1)]
  [TestCase(8)]
  [TestCase(64)]
  [TestCase(65)]
  [TestCase(1027)]
  public void Given_BuffersDifferingInFirstByte_When_Comparing_Then_TheyDiffer(int length) {
    var a = new byte[length];
    new Random(length).NextBytes(a);
    var b = (byte[])a.Clone();
    b[0] ^= 0xFF;

    Assert.That(BlockComparer.IsEqual(a, length, b, length), Is.False);
  }

  [TestCase(1)]
  [TestCase(8)]
  [TestCase(64)]
  [TestCase(65)]
  [TestCase(1027)]
  public void Given_BuffersDifferingInLastByte_When_Comparing_Then_TheyDiffer(int length) {
    var a = new byte[length];
    new Random(length).NextBytes(a);
    var b = (byte[])a.Clone();
    b[length - 1] ^= 0xFF;

    Assert.That(BlockComparer.IsEqual(a, length, b, length), Is.False);
  }

  [Test]
  public void Given_DifferentLengths_When_Comparing_Then_TheyDiffer()
    => Assert.That(BlockComparer.IsEqual(new byte[4], 4, new byte[5], 5), Is.False);

  [Test]
  public void Given_SameArrayInstance_When_Comparing_Then_TheyAreEqual() {
    var a = new byte[16];

    Assert.That(BlockComparer.IsEqual(a, 16, a, 16), Is.True);
  }

}
