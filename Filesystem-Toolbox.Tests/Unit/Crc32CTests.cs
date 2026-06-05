using System.Text;
using Filesystem_Toolbox.Core.Redundancy;

namespace Filesystem_Toolbox.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class Crc32CTests {

  [Test]
  public void Given_StandardCheckVector_When_Computing_Then_KnownCrcIsProduced() {

    // the canonical CRC-32C check value for "123456789"
    var data = Encoding.ASCII.GetBytes("123456789");

    Assert.That(Crc32C.Compute(data, 0, data.Length), Is.EqualTo(0xE3069283u));
  }

  [Test]
  public void Given_EmptyInput_When_Computing_Then_CrcIsZero()
    => Assert.That(Crc32C.Compute([], 0, 0), Is.Zero);

  [Test]
  public void Given_Data_When_SingleBitFlips_Then_CrcDiffers() {
    var data = new byte[256];
    new Random(5).NextBytes(data);
    var original = Crc32C.Compute(data, 0, data.Length);

    data[128] ^= 0x01;

    Assert.That(Crc32C.Compute(data, 0, data.Length), Is.Not.EqualTo(original));
  }

  [Test]
  public void Given_ChunkedInput_When_ChainingWithSeed_Then_ResultEqualsWholeInput() {
    var data = new byte[1000];
    new Random(11).NextBytes(data);
    var whole = Crc32C.Compute(data, 0, data.Length);

    var chained = Crc32C.Compute(data, 0, 333);
    chained = Crc32C.Compute(chained, data, 333, 333);
    chained = Crc32C.Compute(chained, data, 666, 334);

    Assert.That(chained, Is.EqualTo(whole));
  }

  [Test]
  public void Given_InvalidBounds_When_Computing_Then_ExceptionIsThrown() {
    Assert.Multiple(() => {
      Assert.That(() => Crc32C.Compute(null!, 0, 1), Throws.ArgumentNullException);
      Assert.That(() => Crc32C.Compute(new byte[4], 2, 4), Throws.InstanceOf<ArgumentOutOfRangeException>());
    });
  }

}
