using Filesystem_Toolbox.Core.Redundancy;

namespace Filesystem_Toolbox.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class ParityGeometryTests {

  private const int _SHARD = ParityGeometry.DEFAULT_SHARD_SIZE;        // 64 KiB
  private const long _STRIPE = (long)_SHARD * ParityGeometry.DEFAULT_DATA_SHARDS; // 1 MiB

  [TestCase(0, 1, Description = "0% still keeps one parity shard (clamped)")]
  [TestCase(1, 1)]
  [TestCase(7, 2, Description = "ceil(16*0.07)=2")]
  [TestCase(25, 4)]
  [TestCase(50, 8)]
  [TestCase(100, 16)]
  [TestCase(10000, 239, Description = "clamped to GF(2^8) limit 255-16")]
  public void Given_RedundancyPercent_When_DerivingGeometry_Then_ParityShardCountIsClampedCeil(int percent, int expectedParityShards)
    => Assert.That(ParityGeometry.FromRedundancyPercent(percent).ParityShardCount, Is.EqualTo(expectedParityShards));

  [Test]
  public void Given_NegativePercent_When_DerivingGeometry_Then_ArgumentOutOfRangeExceptionIsThrown()
    => Assert.That(() => ParityGeometry.FromRedundancyPercent(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());

  [TestCase(0L, 0L)]
  [TestCase(1L, 1L)]
  [TestCase((long)_SHARD - 1, 1L)]
  [TestCase((long)_SHARD, 1L)]
  [TestCase(_STRIPE - 1, 1L)]
  [TestCase(_STRIPE, 1L)]
  [TestCase(_STRIPE + 1, 2L)]
  [TestCase(50L * 1024 * 1024 * 1024, 51200L, Description = "50 GiB streams as 51200 bounded stripes")]
  public void Given_FileLength_When_ComputingStripeCount_Then_BoundaryValuesMatch(long fileLength, long expectedStripes)
    => Assert.That(ParityGeometry.FromRedundancyPercent(25).GetStripeCount(fileLength), Is.EqualTo(expectedStripes));

  [Test]
  public void Given_NegativeFileLength_When_ComputingStripeCount_Then_ArgumentOutOfRangeExceptionIsThrown()
    => Assert.That(() => ParityGeometry.FromRedundancyPercent(25).GetStripeCount(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());

  [Test]
  public void Given_InvalidShardConfiguration_When_Constructing_Then_ArgumentOutOfRangeExceptionIsThrown() {
    Assert.Multiple(() => {
      Assert.That(() => new ParityGeometry(0, 16, 4), Throws.InstanceOf<ArgumentOutOfRangeException>());
      Assert.That(() => new ParityGeometry(_SHARD, 0, 4), Throws.InstanceOf<ArgumentOutOfRangeException>());
      Assert.That(() => new ParityGeometry(_SHARD, 16, 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
      Assert.That(() => new ParityGeometry(_SHARD, 200, 56), Throws.InstanceOf<ArgumentOutOfRangeException>());
    });
  }

}
