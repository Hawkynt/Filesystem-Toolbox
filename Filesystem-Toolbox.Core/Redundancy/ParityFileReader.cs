using System;
using System.IO;

namespace Filesystem_Toolbox.Core.Redundancy {

  /// <summary>
  /// Opens and validates a parity file: magic, version, geometry sanity, header CRC and the
  /// length implied by the header are checked up front (throwing <see cref="ParityFormatException"/>),
  /// while payload corruption is *not* an error here - it surfaces through the per-shard CRCs
  /// so the repair flow can treat damaged shards as erasures.
  /// </summary>
  internal sealed class ParityFileReader : IDisposable {

    private readonly FileStream _stream;
    private readonly byte[] _crcTable;

    public ParityHeader Header { get; }

    private ParityFileReader(FileStream stream, ParityHeader header, byte[] crcTable) {
      this._stream = stream;
      this.Header = header;
      this._crcTable = crcTable;
    }

    /// <exception cref="ParityFormatException">when the file is structurally unusable</exception>
    public static ParityFileReader Open(FileInfo parityFile) {
      if (parityFile == null) throw new ArgumentNullException(nameof(parityFile));

      var stream = new FileStream(parityFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.RandomAccess);
      try {
        var headerBytes = new byte[ParityFileFormat.HEADER_SIZE + ParityFileFormat.HEADER_CRC_SIZE];
        if (_ReadFully(stream, headerBytes) < headerBytes.Length)
          throw new ParityFormatException("Parity file is shorter than its header");

        for (var i = 0; i < ParityFileFormat.MAGIC.Length; ++i)
          if (headerBytes[i] != ParityFileFormat.MAGIC[i])
            throw new ParityFormatException("Parity file magic mismatch");

        var version = _GetUInt16(headerBytes, 8);
        if (version != ParityFileFormat.FORMAT_VERSION)
          throw new ParityFormatException($"Unsupported parity format version {version}");

        var storedHeaderCrc = _GetUInt32(headerBytes, ParityFileFormat.HEADER_SIZE);
        if (storedHeaderCrc != Crc32C.Compute(headerBytes, 0, ParityFileFormat.HEADER_SIZE))
          throw new ParityFormatException("Parity header checksum mismatch");

        var flags = _GetUInt16(headerBytes, 10);
        var shardSize = (int)_GetUInt32(headerBytes, 12);
        var dataShards = _GetUInt16(headerBytes, 16);
        var parityShards = _GetUInt16(headerBytes, 18);
        var stripeCount = (long)_GetUInt32(headerBytes, 20);
        var originalLength = (long)_GetUInt64(headerBytes, 24);

        if (shardSize < 1 || dataShards < 1 || parityShards < 1 || dataShards + parityShards > 255)
          throw new ParityFormatException("Parity header describes an invalid geometry");

        var sha512 = new byte[ParityFileFormat.SHA512_SIZE];
        Buffer.BlockCopy(headerBytes, 32, sha512, 0, sha512.Length);

        var expectedLength = ParityFileFormat.GetExpectedFileLength(stripeCount, dataShards, parityShards, shardSize);
        if (stream.Length != expectedLength)
          throw new ParityFormatException($"Parity file is {stream.Length} bytes but the header implies {expectedLength} - truncated or padded");

        var crcTable = new byte[ParityFileFormat.GetCrcTableBytes(stripeCount, dataShards, parityShards)];
        if (_ReadFully(stream, crcTable) < crcTable.Length)
          throw new ParityFormatException("Parity CRC table is truncated");

        var header = new ParityHeader(flags, shardSize, dataShards, parityShards, stripeCount, originalLength, sha512);
        return new ParityFileReader(stream, header, crcTable);
      } catch {
        stream.Dispose();
        throw;
      }
    }

    /// <summary>
    /// The recorded CRC-32C of a shard; data shards are indexed 0..k-1, parity shards k..k+m-1.
    /// </summary>
    public uint GetShardCrc(long stripe, int shardIndex) {
      var header = this.Header;
      if (stripe < 0 || stripe >= header.StripeCount) throw new ArgumentOutOfRangeException(nameof(stripe));
      if (shardIndex < 0 || shardIndex >= header.DataShards + header.ParityShards) throw new ArgumentOutOfRangeException(nameof(shardIndex));

      var offset = (stripe * (header.DataShards + header.ParityShards) + shardIndex) * sizeof(uint);
      return _GetUInt32(this._crcTable, (int)offset);
    }

    /// <summary>Reads parity shard <paramref name="parityIndex"/> (0..m-1) of the given stripe.</summary>
    public void ReadParityShard(long stripe, int parityIndex, byte[] buffer) {
      var header = this.Header;
      if (stripe < 0 || stripe >= header.StripeCount) throw new ArgumentOutOfRangeException(nameof(stripe));
      if (parityIndex < 0 || parityIndex >= header.ParityShards) throw new ArgumentOutOfRangeException(nameof(parityIndex));
      if (buffer == null) throw new ArgumentNullException(nameof(buffer));
      if (buffer.Length < header.ShardSize) throw new ArgumentException("buffer is smaller than a shard", nameof(buffer));

      this._stream.Position =
        ParityFileFormat.GetPayloadOffset(header.StripeCount, header.DataShards, header.ParityShards)
        + (stripe * header.ParityShards + parityIndex) * (long)header.ShardSize;

      if (_ReadFully(this._stream, buffer, header.ShardSize) < header.ShardSize)
        throw new ParityFormatException("Unexpected end of parity payload");
    }

    /// <summary>Recomputes the whole-payload checksum; <c>false</c> signals corrupt parity payload.</summary>
    public bool VerifyPayloadCrc() {
      var header = this.Header;
      var payloadBytes = ParityFileFormat.GetPayloadBytes(header.StripeCount, header.ParityShards, header.ShardSize);
      this._stream.Position = ParityFileFormat.GetPayloadOffset(header.StripeCount, header.DataShards, header.ParityShards);

      uint crc = 0;
      var buffer = new byte[1 << 16];
      var remaining = payloadBytes;
      while (remaining > 0) {
        var chunk = (int)Math.Min(buffer.Length, remaining);
        if (_ReadFully(this._stream, buffer, chunk) < chunk)
          return false;

        crc = Crc32C.Compute(crc, buffer, 0, chunk);
        remaining -= chunk;
      }

      var trailer = new byte[ParityFileFormat.TRAILER_SIZE];
      if (_ReadFully(this._stream, trailer) < trailer.Length)
        return false;

      return _GetUInt32(trailer, 0) == crc;
    }

    public void Dispose() => this._stream.Dispose();

    private static int _ReadFully(Stream stream, byte[] buffer) => _ReadFully(stream, buffer, buffer.Length);

    private static int _ReadFully(Stream stream, byte[] buffer, int count) {
      var total = 0;
      while (total < count) {
        var read = stream.Read(buffer, total, count - total);
        if (read == 0)
          break;

        total += read;
      }

      return total;
    }

    private static ushort _GetUInt16(byte[] buffer, int offset) => (ushort)(buffer[offset] | buffer[offset + 1] << 8);

    private static uint _GetUInt32(byte[] buffer, int offset)
      => (uint)(buffer[offset] | buffer[offset + 1] << 8 | buffer[offset + 2] << 16 | buffer[offset + 3] << 24);

    private static ulong _GetUInt64(byte[] buffer, int offset)
      => _GetUInt32(buffer, offset) | (ulong)_GetUInt32(buffer, offset + 4) << 32;

  }
}
