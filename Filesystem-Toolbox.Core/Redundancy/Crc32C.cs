using System;

namespace Filesystem_Toolbox.Core.Redundancy {

  /// <summary>
  /// CRC-32C (Castagnoli, reflected polynomial 0x82F63B78) - software table implementation,
  /// deliberately portable across net48 and net8.0 instead of relying on CPU intrinsics.
  /// </summary>
  internal static class Crc32C {

    private const uint _POLYNOMIAL = 0x82F63B78;
    private static readonly uint[] _TABLE = _BuildTable();

    private static uint[] _BuildTable() {
      var result = new uint[256];
      for (uint i = 0; i < 256; ++i) {
        var crc = i;
        for (var bit = 0; bit < 8; ++bit)
          crc = (crc >> 1) ^ (_POLYNOMIAL & (uint)-(int)(crc & 1));

        result[i] = crc;
      }

      return result;
    }

    public static uint Compute(byte[] data, int offset, int length) => Compute(0, data, offset, length);

    /// <summary>
    /// Continues a checksum: pass the result of a previous call as <paramref name="seed"/>
    /// to checksum data arriving in chunks.
    /// </summary>
    public static uint Compute(uint seed, byte[] data, int offset, int length) {
      if (data == null) throw new ArgumentNullException(nameof(data));
      if (offset < 0 || length < 0 || offset + length > data.Length) throw new ArgumentOutOfRangeException(nameof(offset));

      var crc = ~seed;
      for (var i = offset; i < offset + length; ++i)
        crc = _TABLE[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);

      return ~crc;
    }

  }
}
