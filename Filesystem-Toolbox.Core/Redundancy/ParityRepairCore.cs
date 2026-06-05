using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace Filesystem_Toolbox.Core.Redundancy {

  /// <summary>
  /// Stripe-wise erasure repair of a single file against its parity file, with no dependency
  /// on a checksum database: the parity header is fully self-describing (geometry, original
  /// length, bound SHA-512), so even the expected content hash can come from the parity itself.
  /// The repaired content is verified against that hash before the file is atomically replaced -
  /// a wrong result is never shipped.
  /// </summary>
  internal static class ParityRepairCore {

    public readonly struct Outcome {

      public bool Repaired { get; }
      public long BadShards { get; }
      public long StripesRepaired { get; }

      /// <summary>Parity shards themselves were damaged - the caller should rebuild the parity from the healed data.</summary>
      public bool ParityWasDamaged { get; }

      public Outcome(bool repaired, long badShards, long stripesRepaired, bool parityWasDamaged) {
        this.Repaired = repaired;
        this.BadShards = badShards;
        this.StripesRepaired = stripesRepaired;
        this.ParityWasDamaged = parityWasDamaged;
      }

    }

    /// <summary>
    /// Repairs <paramref name="file"/> in place so its content matches the expected hash.
    /// <paramref name="expectedHash"/> overrides the parity header's hash - callers with a
    /// database entry pass it so STALE parity (bound to other content) is refused; <c>null</c>
    /// trusts the header (the self-healing database case).
    /// </summary>
    /// <exception cref="ParityFormatException">the parity file is structurally unusable</exception>
    public static Outcome TryRepairFile(FileInfo file, FileInfo parityFile, byte[] expectedHash = null, CancellationToken token = default) {
      if (file == null) throw new ArgumentNullException(nameof(file));
      if (parityFile == null) throw new ArgumentNullException(nameof(parityFile));

      long badShardsTotal = 0, stripesRepaired = 0;
      var parityWasDamaged = false;

      using (var reader = ParityFileReader.Open(parityFile)) {

        // stale parity must never "repair" a file towards outdated content
        if (expectedHash != null && !reader.Header.OriginalSha512.SequenceEqual(expectedHash))
          return new Outcome(false, 0, 0, false);

        var targetHash = expectedHash ?? reader.Header.OriginalSha512;

        var header = reader.Header;
        var k = header.DataShards;
        var m = header.ParityShards;
        var shardSize = header.ShardSize;
        var codec = new ReedSolomonCodec(k, m);

        var shards = new byte[k + m][];
        for (var i = 0; i < shards.Length; ++i)
          shards[i] = new byte[shardSize];

        var present = new bool[k + m];

        var temporary = new FileInfo(file.FullName + ".fst-repair");
        try {
          byte[] repairedHash;
          using (var damagedStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan))
          using (var repairedStream = new FileStream(temporary.FullName, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
          using (var sha512 = SHA512.Create()) {
            var remaining = header.OriginalLength;

            for (long stripe = 0; stripe < header.StripeCount; ++stripe) {
              token.ThrowIfCancellationRequested();

              // read this stripe's data shards from the damaged file; CRC mismatches become erasures
              var stripeDamaged = false;
              for (var shard = 0; shard < k; ++shard) {
                var buffer = shards[shard];
                var read = _ReadFully(damagedStream, buffer);
                if (read < shardSize)
                  Array.Clear(buffer, read, shardSize - read);

                present[shard] = Crc32C.Compute(buffer, 0, shardSize) == reader.GetShardCrc(stripe, shard);
                if (present[shard])
                  continue;

                stripeDamaged = true;
                ++badShardsTotal;
              }

              if (stripeDamaged) {
                for (var parityIndex = 0; parityIndex < m; ++parityIndex) {
                  var buffer = shards[k + parityIndex];
                  reader.ReadParityShard(stripe, parityIndex, buffer);
                  present[k + parityIndex] = Crc32C.Compute(buffer, 0, shardSize) == reader.GetShardCrc(stripe, k + parityIndex);
                  if (!present[k + parityIndex]) {
                    parityWasDamaged = true;
                    ++badShardsTotal;
                  }
                }

                if (!codec.DecodeErasures(shards, present, shardSize))
                  return new Outcome(false, badShardsTotal, stripesRepaired, parityWasDamaged); // more erasures than parity can cover

                ++stripesRepaired;
              }

              // emit the (possibly reconstructed) data shards, trimmed to the original length
              for (var shard = 0; shard < k && remaining > 0; ++shard) {
                var chunk = (int)Math.Min(shardSize, remaining);
                repairedStream.Write(shards[shard], 0, chunk);
                sha512.TransformBlock(shards[shard], 0, chunk, null, 0);
                remaining -= chunk;
              }
            }

            sha512.TransformFinalBlock([], 0, 0);
            repairedHash = sha512.Hash;
          }

          // never ship an unverified result - the reconstructed file must match the expected hash exactly
          if (!repairedHash.SequenceEqual(targetHash))
            return new Outcome(false, badShardsTotal, stripesRepaired, parityWasDamaged);

          file.Refresh();
          var attributes = file.Exists ? file.Attributes : FileAttributes.Normal;
          if (file.Exists)
            file.Delete();

          File.Move(temporary.FullName, file.FullName);

          try {
            if (attributes != FileAttributes.Normal)
              file.Attributes = attributes;
          } catch (IOException) {
            ;
          } catch (UnauthorizedAccessException) {
            ;
          }
        } finally {
          temporary.Refresh();
          if (temporary.Exists)
            temporary.Delete();
        }

        return new Outcome(true, badShardsTotal, stripesRepaired, parityWasDamaged);
      }
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

  }
}
