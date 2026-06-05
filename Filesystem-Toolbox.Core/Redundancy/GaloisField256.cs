using System;

namespace Filesystem_Toolbox.Core.Redundancy {

  /// <summary>
  /// Arithmetic over GF(2^8) with the primitive polynomial x^8+x^4+x^3+x^2+1 (0x11D),
  /// the field used by classic Reed-Solomon storage codes.
  /// </summary>
  internal static class GaloisField256 {

    private const int _PRIMITIVE_POLYNOMIAL = 0x11D;
    private const int _FIELD_SIZE = 256;
    private const int _GROUP_ORDER = _FIELD_SIZE - 1;

    /// <summary>Powers of the generator (length doubled so multiplication needs no modulo).</summary>
    private static readonly byte[] _EXP = new byte[2 * _GROUP_ORDER];

    /// <summary>Discrete logarithms; index 0 is invalid (log of zero is undefined).</summary>
    private static readonly byte[] _LOG = new byte[_FIELD_SIZE];

    /// <summary>Full 256x256 multiplication table; row [a] holds a*b for every b.</summary>
    private static readonly byte[][] _MUL = new byte[_FIELD_SIZE][];

    static GaloisField256() {
      var x = 1;
      for (var i = 0; i < _GROUP_ORDER; ++i) {
        _EXP[i] = (byte)x;
        _EXP[i + _GROUP_ORDER] = (byte)x;
        _LOG[x] = (byte)i;
        x <<= 1;
        if (x >= _FIELD_SIZE)
          x ^= _PRIMITIVE_POLYNOMIAL;
      }

      for (var a = 0; a < _FIELD_SIZE; ++a) {
        var row = _MUL[a] = new byte[_FIELD_SIZE];
        if (a == 0)
          continue;

        for (var b = 1; b < _FIELD_SIZE; ++b)
          row[b] = _EXP[_LOG[a] + _LOG[b]];
      }
    }

    public static byte Add(byte a, byte b) => (byte)(a ^ b);

    public static byte Subtract(byte a, byte b) => (byte)(a ^ b);

    public static byte Multiply(byte a, byte b) => _MUL[a][b];

    /// <exception cref="DivideByZeroException">when <paramref name="b"/> is zero</exception>
    public static byte Divide(byte a, byte b) {
      if (b == 0)
        throw new DivideByZeroException("Division by zero in GF(2^8)");

      return a == 0 ? (byte)0 : _EXP[_LOG[a] + _GROUP_ORDER - _LOG[b]];
    }

    /// <exception cref="DivideByZeroException">when <paramref name="a"/> is zero</exception>
    public static byte Inverse(byte a) {
      if (a == 0)
        throw new DivideByZeroException("Zero has no multiplicative inverse in GF(2^8)");

      return _EXP[_GROUP_ORDER - _LOG[a]];
    }

    public static byte Power(byte a, int exponent) {
      if (exponent == 0)
        return 1;

      if (a == 0)
        return 0;

      var log = _LOG[a] * (long)exponent % _GROUP_ORDER;
      if (log < 0)
        log += _GROUP_ORDER;

      return _EXP[log];
    }

    /// <summary>
    /// The Reed-Solomon hot kernel: dst[i] ^= scalar * src[i] for a whole shard region.
    /// </summary>
    public static unsafe void MultiplyAndAddRegion(byte scalar, byte[] source, int sourceOffset, byte[] destination, int destinationOffset, int length) {
      if (source == null) throw new ArgumentNullException(nameof(source));
      if (destination == null) throw new ArgumentNullException(nameof(destination));
      if (sourceOffset < 0 || sourceOffset + length > source.Length) throw new ArgumentOutOfRangeException(nameof(sourceOffset));
      if (destinationOffset < 0 || destinationOffset + length > destination.Length) throw new ArgumentOutOfRangeException(nameof(destinationOffset));

      if (scalar == 0 || length <= 0)
        return;

      var row = _MUL[scalar];
      fixed (byte* sourceFix = &source[sourceOffset], destinationFix = &destination[destinationOffset], rowFix = row) {
        var sourcePointer = sourceFix;
        var destinationPointer = destinationFix;
        var i = length;

        // unrolled by eight - this loop dominates encode and decode time
        for (; i >= 8; i -= 8, sourcePointer += 8, destinationPointer += 8) {
          destinationPointer[0] ^= rowFix[sourcePointer[0]];
          destinationPointer[1] ^= rowFix[sourcePointer[1]];
          destinationPointer[2] ^= rowFix[sourcePointer[2]];
          destinationPointer[3] ^= rowFix[sourcePointer[3]];
          destinationPointer[4] ^= rowFix[sourcePointer[4]];
          destinationPointer[5] ^= rowFix[sourcePointer[5]];
          destinationPointer[6] ^= rowFix[sourcePointer[6]];
          destinationPointer[7] ^= rowFix[sourcePointer[7]];
        }

        for (; i > 0; --i, ++sourcePointer, ++destinationPointer)
          *destinationPointer ^= rowFix[*sourcePointer];
      }
    }

  }
}
