using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Filesystem_Toolbox.Core.Redundancy {

  /// <summary>
  /// Streams a source file stripe by stripe, encodes Reed-Solomon parity and writes a
  /// parity file (memory stays bounded by one stripe regardless of file size).
  /// The destination is written atomically via a temporary sibling file.
  /// </summary>
  internal sealed class ParityFileWriter {

    private readonly ParityGeometry _geometry;

    public ParityFileWriter(ParityGeometry geometry) => this._geometry = geometry;

    /// <summary>
    /// Writes parity for <paramref name="source"/> to <paramref name="destination"/>.
    /// </summary>
    /// <returns>The SHA-512 of the source as it was streamed - the content state the parity is bound to.</returns>
    public byte[] Write(FileInfo source, FileInfo destination, CancellationToken token = default) {
      if (source == null) throw new ArgumentNullException(nameof(source));
      if (destination == null) throw new ArgumentNullException(nameof(destination));

      var geometry = this._geometry;
      var k = geometry.DataShards;
      var m = geometry.ParityShardCount;
      var shardSize = geometry.ShardSize;
      var codec = new ReedSolomonCodec(k, m);

      var dataShards = new byte[k][];
      for (var i = 0; i < k; ++i)
        dataShards[i] = new byte[shardSize];

    var parityShards = new byte[m][];
      for (var i = 0; i < m; ++i)
        parityShards[i] = new byte[shardSize];

      var temporaryFile = new FileInfo(destination.FullName + ".tmp");
      try {
        byte[] hash;
        long stripeCount;
        byte[] crcTable;
        uint payloadCrc = 0;

        using (var sourceStream = new FileStream(source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan))
        using (var sha512 = SHA512.Create())
        using (var destinationStream = new FileStream(temporaryFile.FullName, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1 << 16)) {
          var length = sourceStream.Length;
          stripeCount = geometry.GetStripeCount(length);
          crcTable = new byte[ParityFileFormat.GetCrcTableBytes(stripeCount, k, m)];

          // reserve space for header, header crc and crc table; both get written afterwards
          destinationStream.SetLength(ParityFileFormat.GetExpectedFileLength(stripeCount, k, m, shardSize));
          destinationStream.Position = ParityFileFormat.GetPayloadOffset(stripeCount, k, m);

          var crcTableIndex = 0;
          for (long stripe = 0; stripe < stripeCount; ++stripe) {
            token.ThrowIfCancellationRequested();

            for (var shard = 0; shard < k; ++shard) {
              var buffer = dataShards[shard];
              var read = _ReadFully(sourceStream, buffer);
              if (read > 0)
                sha512.TransformBlock(buffer, 0, read, null, 0);

              if (read < buffer.Length)
                Array.Clear(buffer, read, buffer.Length - read);

              _WriteCrc(crcTable, ref crcTableIndex, Crc32C.Compute(buffer, 0, shardSize));
            }

            codec.Encode(dataShards, parityShards, shardSize);

            foreach (var parityShard in parityShards) {
              _WriteCrc(crcTable, ref crcTableIndex, Crc32C.Compute(parityShard, 0, shardSize));
              payloadCrc = Crc32C.Compute(payloadCrc, parityShard, 0, shardSize);
              destinationStream.Write(parityShard, 0, shardSize);
            }
          }

          sha512.TransformFinalBlock([], 0, 0);
          hash = sha512.Hash;

          // trailer
          var trailer = new byte[ParityFileFormat.TRAILER_SIZE];
          _PutUInt32(trailer, 0, payloadCrc);
          Buffer.BlockCopy(ParityFileFormat.END_MAGIC, 0, trailer, 4, 4);
          destinationStream.Write(trailer, 0, trailer.Length);

          // header + header crc + crc table
          var header = _BuildHeader(geometry, stripeCount, length, hash);
          destinationStream.Position = 0;
          destinationStream.Write(header, 0, header.Length);
          destinationStream.Write(crcTable, 0, crcTable.Length);
          destinationStream.Flush(true);
        }

        destination.Refresh();
        if (destination.Exists)
          destination.Delete();

        File.Move(temporaryFile.FullName, destination.FullName);
        return hash;
      } catch {
        temporaryFile.Refresh();
        if (temporaryFile.Exists)
          temporaryFile.Delete();

        throw;
      }
    }

    private static byte[] _BuildHeader(ParityGeometry geometry, long stripeCount, long originalLength, byte[] sha512) {
      var result = new byte[ParityFileFormat.HEADER_SIZE + ParityFileFormat.HEADER_CRC_SIZE];
      Buffer.BlockCopy(ParityFileFormat.MAGIC, 0, result, 0, 8);
      _PutUInt16(result, 8, ParityFileFormat.FORMAT_VERSION);
      _PutUInt16(result, 10, 0 /* flags */);
      _PutUInt32(result, 12, (uint)geometry.ShardSize);
      _PutUInt16(result, 16, (ushort)geometry.DataShards);
      _PutUInt16(result, 18, (ushort)geometry.ParityShardCount);
      _PutUInt32(result, 20, (uint)stripeCount);
      _PutUInt64(result, 24, (ulong)originalLength);
      Buffer.BlockCopy(sha512, 0, result, 32, ParityFileFormat.SHA512_SIZE);
      _PutUInt32(result, ParityFileFormat.HEADER_SIZE, Crc32C.Compute(result, 0, ParityFileFormat.HEADER_SIZE));
      return result;
    }

    private static int _ReadFully(Stream stream, byte[] buffer) {
      var total = 0;
      while (total < buffer.Length) {
        var read = stream.Read(buffer, total, buffer.Length - total);
        if (read == 0)
          break;

        total += read;
      }

      return total;
    }

    private static void _WriteCrc(byte[] table, ref int index, uint crc) {
      _PutUInt32(table, index, crc);
      index += sizeof(uint);
    }

    private static void _PutUInt16(byte[] buffer, int offset, ushort value) {
      buffer[offset] = (byte)value;
      buffer[offset + 1] = (byte)(value >> 8);
    }

    private static void _PutUInt32(byte[] buffer, int offset, uint value) {
      buffer[offset] = (byte)value;
      buffer[offset + 1] = (byte)(value >> 8);
      buffer[offset + 2] = (byte)(value >> 16);
      buffer[offset + 3] = (byte)(value >> 24);
    }

    private static void _PutUInt64(byte[] buffer, int offset, ulong value) {
      _PutUInt32(buffer, offset, (uint)value);
      _PutUInt32(buffer, offset + 4, (uint)(value >> 32));
    }

  }
}
