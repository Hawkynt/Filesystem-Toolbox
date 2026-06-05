using Filesystem_Toolbox.Core.Redundancy;

namespace Filesystem_Toolbox.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class GaloisField256Tests {

  [TestCase((byte)0, (byte)0, (byte)0)]
  [TestCase((byte)0, (byte)255, (byte)255)]
  [TestCase((byte)1, (byte)1, (byte)0)]
  [TestCase((byte)0xAA, (byte)0x55, (byte)0xFF)]
  public void Given_TwoElements_When_Adding_Then_ResultIsXor(byte a, byte b, byte expected) {
    Assert.That(GaloisField256.Add(a, b), Is.EqualTo(expected));
    Assert.That(GaloisField256.Subtract(a, b), Is.EqualTo(expected), "subtraction equals addition in GF(2^n)");
  }

  [Test]
  public void Given_AnyElement_When_MultipliedByZero_Then_ResultIsZero() {
    for (var a = 0; a < 256; ++a)
      Assert.That(GaloisField256.Multiply((byte)a, 0), Is.Zero);
  }

  [Test]
  public void Given_AnyElement_When_MultipliedByOne_Then_ElementIsUnchanged() {
    for (var a = 0; a < 256; ++a)
      Assert.That(GaloisField256.Multiply((byte)a, 1), Is.EqualTo((byte)a));
  }

  [Test]
  public void Given_KnownVector_When_Multiplying_Then_MatchesReferencePolynomialMultiplication() {

    // bit-by-bit carry-less multiplication modulo 0x11D as independent reference
    static byte Reference(byte a, byte b) {
      var result = 0;
      var aa = (int)a;
      for (var bb = (int)b; bb != 0; bb >>= 1) {
        if ((bb & 1) != 0)
          result ^= aa;

        aa <<= 1;
        if ((aa & 0x100) != 0)
          aa ^= 0x11D;
      }

      return (byte)result;
    }

    for (var a = 0; a < 256; ++a)
    for (var b = 0; b < 256; ++b)
      Assert.That(GaloisField256.Multiply((byte)a, (byte)b), Is.EqualTo(Reference((byte)a, (byte)b)), $"{a} * {b}");
  }

  [Test]
  public void Given_NonZeroElements_When_MultiplyingThenDividing_Then_OriginalIsRecovered() {
    for (var a = 1; a < 256; ++a)
    for (var b = 1; b < 256; ++b) {
      var product = GaloisField256.Multiply((byte)a, (byte)b);
      Assert.That(GaloisField256.Divide(product, (byte)b), Is.EqualTo((byte)a), $"({a} * {b}) / {b}");
    }
  }

  [Test]
  public void Given_AnyElement_When_DividingByZero_Then_DivideByZeroExceptionIsThrown()
    => Assert.That(() => GaloisField256.Divide(42, 0), Throws.InstanceOf<DivideByZeroException>());

  [Test]
  public void Given_NonZeroElement_When_MultipliedByItsInverse_Then_ResultIsOne() {
    for (var a = 1; a < 256; ++a)
      Assert.That(GaloisField256.Multiply((byte)a, GaloisField256.Inverse((byte)a)), Is.EqualTo((byte)1), $"a={a}");
  }

  [Test]
  public void Given_Zero_When_Inverting_Then_DivideByZeroExceptionIsThrown()
    => Assert.That(() => GaloisField256.Inverse(0), Throws.InstanceOf<DivideByZeroException>());

  [Test]
  public void Given_Element_When_RaisedToPowers_Then_MatchesRepeatedMultiplication() {
    for (var a = 0; a < 256; ++a) {
      byte expected = 1;
      for (var exponent = 0; exponent < 10; ++exponent) {
        Assert.That(GaloisField256.Power((byte)a, exponent), Is.EqualTo(expected), $"{a}^{exponent}");
        expected = GaloisField256.Multiply(expected, (byte)a);
      }
    }
  }

  [Test]
  public void Given_Region_When_MultiplyAndAdd_Then_MatchesScalarReferenceLoop() {
    var random = new Random(1234);
    var source = new byte[1027]; // deliberately not a multiple of the unroll factor
    var destination = new byte[1027];
    random.NextBytes(source);
    random.NextBytes(destination);
    const byte scalar = 0x8E;

    var expected = new byte[destination.Length];
    for (var i = 0; i < destination.Length; ++i)
      expected[i] = (byte)(destination[i] ^ GaloisField256.Multiply(scalar, source[i]));

    GaloisField256.MultiplyAndAddRegion(scalar, source, 0, destination, 0, source.Length);

    Assert.That(destination, Is.EqualTo(expected));
  }

  [Test]
  public void Given_ZeroScalar_When_MultiplyAndAdd_Then_DestinationIsUnchanged() {
    var source = new byte[64];
    var destination = new byte[64];
    new Random(7).NextBytes(destination);
    var expected = (byte[])destination.Clone();

    GaloisField256.MultiplyAndAddRegion(0, source, 0, destination, 0, 64);

    Assert.That(destination, Is.EqualTo(expected));
  }

  [Test]
  public void Given_InvalidRegionBounds_When_MultiplyAndAdd_Then_ArgumentExceptionIsThrown() {
    var buffer = new byte[16];

    Assert.Multiple(() => {
      Assert.That(() => GaloisField256.MultiplyAndAddRegion(1, null!, 0, buffer, 0, 16), Throws.ArgumentNullException);
      Assert.That(() => GaloisField256.MultiplyAndAddRegion(1, buffer, 0, null!, 0, 16), Throws.ArgumentNullException);
      Assert.That(() => GaloisField256.MultiplyAndAddRegion(1, buffer, 8, buffer, 0, 16), Throws.InstanceOf<ArgumentOutOfRangeException>());
    });
  }

}
