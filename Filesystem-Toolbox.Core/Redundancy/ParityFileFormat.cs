using System;

namespace Filesystem_Toolbox.Core.Redundancy {

  /// <summary>Thrown when a parity file is structurally unusable (bad magic, version, length or header checksum).</summary>
  public sealed class ParityFormatException : Exception {
    public ParityFormatException(string message) : base(message) { }
  }

  /// <summary>
  /// Byte layout of a parity file (*.par), version 1, little-endian:
  /// <code>
  ///   0  8  magic "FSTPAR\0\0"
  ///   8  2  format version (1)
  ///  10  2  flags (reserved)
  ///  12  4  shard size in bytes
  ///  16  2  data shards per stripe (k)
  ///  18  2  parity shards per stripe (m)
  ///  20  4  stripe count
  ///  24  8  original file length
  ///  32 64  SHA-512 of the original file (binds parity to exactly one content state)
  ///  96  4  CRC-32C over bytes 0..95
  /// 100  .  per-shard CRC-32C table: stripeCount x (k+m) entries, stripe-major
  ///   .  .  parity payload: stripeCount x m shards of shardSize bytes each
  ///   .  4  CRC-32C over the whole parity payload
  ///   .  4  end magic "PEND"
  /// </code>
  /// The total length is fully determined by the header, so truncation is detected up front.
  /// </summary>
  internal static class ParityFileFormat {

    public static readonly byte[] MAGIC = { (byte)'F', (byte)'S', (byte)'T', (byte)'P', (byte)'A', (byte)'R', 0, 0 };
    public static readonly byte[] END_MAGIC = { (byte)'P', (byte)'E', (byte)'N', (byte)'D' };

    public const ushort FORMAT_VERSION = 1;
    public const int HEADER_SIZE = 96;
    public const int HEADER_CRC_SIZE = 4;
    public const int CRC_TABLE_OFFSET = HEADER_SIZE + HEADER_CRC_SIZE;
    public const int TRAILER_SIZE = 8;
    public const int SHA512_SIZE = 64;

    public static long GetCrcTableBytes(long stripeCount, int dataShards, int parityShards)
      => stripeCount * (dataShards + parityShards) * sizeof(uint);

    public static long GetPayloadOffset(long stripeCount, int dataShards, int parityShards)
      => CRC_TABLE_OFFSET + GetCrcTableBytes(stripeCount, dataShards, parityShards);

    public static long GetPayloadBytes(long stripeCount, int parityShards, int shardSize)
      => stripeCount * parityShards * shardSize;

    public static long GetExpectedFileLength(long stripeCount, int dataShards, int parityShards, int shardSize)
      => GetPayloadOffset(stripeCount, dataShards, parityShards)
       + GetPayloadBytes(stripeCount, parityShards, shardSize)
       + TRAILER_SIZE;

  }

  /// <summary>The parsed, validated header of a parity file.</summary>
  internal sealed class ParityHeader {

    public ushort Flags { get; }
    public int ShardSize { get; }
    public int DataShards { get; }
    public int ParityShards { get; }
    public long StripeCount { get; }
    public long OriginalLength { get; }
    public byte[] OriginalSha512 { get; }

    public ParityHeader(ushort flags, int shardSize, int dataShards, int parityShards, long stripeCount, long originalLength, byte[] originalSha512) {
      this.Flags = flags;
      this.ShardSize = shardSize;
      this.DataShards = dataShards;
      this.ParityShards = parityShards;
      this.StripeCount = stripeCount;
      this.OriginalLength = originalLength;
      this.OriginalSha512 = originalSha512 ?? throw new ArgumentNullException(nameof(originalSha512));
    }

    public ParityGeometry Geometry => new ParityGeometry(this.ShardSize, this.DataShards, this.ParityShards);

  }
}
