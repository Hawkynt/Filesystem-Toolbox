using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Filesystem_Toolbox.Core.Integrity;
using Filesystem_Toolbox.Core.Redundancy;

namespace Filesystem_Toolbox.Core.Services {

  public enum RepairResult {

    /// <summary>Nothing wrong - or nothing tracked - for this file.</summary>
    NotNeeded,

    /// <summary>Reconstructed from Reed-Solomon parity; the full SHA-512 was re-verified before replacing.</summary>
    Repaired,

    /// <summary>Restored as a whole from the mirror copy (hash-verified).</summary>
    RepairedFromMirror,

    /// <summary>The file was fine but its parity was stale or damaged - parity was rebuilt.</summary>
    ParityRebuilt,

    /// <summary>The file was intentionally edited; repairing backwards is refused - accept the change instead.</summary>
    ModifiedNotRepaired,

    /// <summary>No parity exists for this file and no mirror could help.</summary>
    ParityMissing,

    /// <summary>Too much damage for the available parity and no usable mirror.</summary>
    Unrepairable,

    /// <summary>The repair attempt itself failed with an exception.</summary>
    Error,

  }

  public sealed class RepairOutcome {

    public FileInfo File { get; }
    public RepairResult Result { get; }
    public long BadShardsFound { get; }
    public long StripesRepaired { get; }
    public Exception Error { get; }

    public RepairOutcome(FileInfo file, RepairResult result, long badShardsFound = 0, long stripesRepaired = 0, Exception error = null) {
      this.File = file;
      this.Result = result;
      this.BadShardsFound = badShardsFound;
      this.StripesRepaired = stripesRepaired;
      this.Error = error;
    }

  }

  /// <summary>
  /// Repairs bit rot using the parity store, falling back to a mirror copy where parity
  /// cannot help. Stale parity (bound to a different content state than the database
  /// records) is never used - repairing a file towards outdated content would itself
  /// be data loss.
  /// </summary>
  public sealed class RepairService {

    private readonly FolderIntegrityChecker _checker;
    private readonly ParityStore _parityStore;
    private readonly MirrorService _mirror;

    public RepairService(FolderIntegrityChecker checker, ParityStore parityStore, MirrorService mirror = null) {
      this._checker = checker ?? throw new ArgumentNullException(nameof(checker));
      this._parityStore = parityStore ?? throw new ArgumentNullException(nameof(parityStore));
      this._mirror = mirror;
    }

    public RepairOutcome Repair(FileInfo file, CancellationToken token = default) {
      if (file == null) throw new ArgumentNullException(nameof(file));

      try {
        return this._Repair(file, token);
      } catch (OperationCanceledException) {
        throw;
      } catch (Exception exception) {
        return new RepairOutcome(file, RepairResult.Error, error: exception);
      }
    }

    private RepairOutcome _Repair(FileInfo file, CancellationToken token) {
      if (!this._checker.TryGetEntry(file, out var stored))
        return new RepairOutcome(file, RepairResult.NotNeeded);

      var verifier = new IntegrityVerifier(this._checker, this._parityStore);
      var classification = verifier.Classify(file, stored);

      switch (classification.Status) {
        case VerificationStatus.Ok:
          return new RepairOutcome(file, RepairResult.NotNeeded);

        case VerificationStatus.ParityStale:
          this._parityStore.BuildParity(file, token);
          return new RepairOutcome(file, RepairResult.ParityRebuilt);

        case VerificationStatus.Modified:
          return new RepairOutcome(file, RepairResult.ModifiedNotRepaired);

        case VerificationStatus.Missing:
          return this._TryMirrorRestore(file, stored, 0)
                 ?? new RepairOutcome(file, this._parityStore.HasParity(file) ? RepairResult.Unrepairable : RepairResult.ParityMissing);

        case VerificationStatus.BitRot:
        case VerificationStatus.Error:
          return this._RepairFromParity(file, stored, token);

        default:
          return new RepairOutcome(file, RepairResult.NotNeeded);
      }
    }

    private RepairOutcome _RepairFromParity(FileInfo file, ChecksumEntry stored, CancellationToken token) {
      if (!this._parityStore.HasParity(file))
        return this._TryMirrorRestore(file, stored, 0)
               ?? new RepairOutcome(file, RepairResult.ParityMissing);

      long badShardsTotal = 0, stripesRepaired = 0;
      bool repaired;
      var parityWasDamaged = false;

      // NOTE: the mirror fallback and the parity self-heal both rewrite the parity file,
      //       so they must only run after the reader below released its handle
      try {
        repaired = this._TryRepairUsingParity(file, stored, token, ref badShardsTotal, ref stripesRepaired, ref parityWasDamaged);
      } catch (ParityFormatException) {

        // structurally broken parity - useless for this repair, but rebuildable once the file is healthy again
        repaired = false;
      }

      if (!repaired)
        return this._TryMirrorRestore(file, stored, badShardsTotal)
               ?? new RepairOutcome(file, RepairResult.Unrepairable, badShardsTotal, stripesRepaired);

      // parity self-heal: data is known-good again, so damaged parity shards can be regenerated
      if (parityWasDamaged)
        this._parityStore.BuildParity(file, token);

      return new RepairOutcome(file, RepairResult.Repaired, badShardsTotal, stripesRepaired);
    }

    /// <summary>The actual stripe-wise erasure repair; <c>false</c> means the caller should try the mirror.</summary>
    private bool _TryRepairUsingParity(FileInfo file, ChecksumEntry stored, CancellationToken token, ref long badShardsTotal, ref long stripesRepaired, ref bool parityWasDamaged) {
      using (var reader = this._parityStore.OpenParity(file)) {
        if (!reader.Header.OriginalSha512.SequenceEqual(stored.Hash))
          return false; // stale parity - never repair towards outdated content

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
                  return false; // more erasures than parity can cover in this stripe

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

          // never ship an unverified result - the reconstructed file must match the recorded hash exactly
          if (!repairedHash.SequenceEqual(stored.Hash))
            return false;

          file.Refresh();
          var attributes = file.Exists ? file.Attributes : FileAttributes.Normal;
          if (file.Exists)
            file.Delete();

          File.Move(temporary.FullName, file.FullName);
          this._RestoreMetadata(file, stored, attributes);
        } finally {
          temporary.Refresh();
          if (temporary.Exists)
            temporary.Delete();
        }

        return true;
      }
    }

    private RepairOutcome _TryMirrorRestore(FileInfo file, ChecksumEntry stored, long badShardsFound) {
      if (this._mirror == null)
        return null;

      byte[] hash;
      try {
        hash = stored.Hash;
      } catch (FormatException) {
        return null;
      }

      if (!this._mirror.Restore(file, hash))
        return null;

      this._RestoreMetadata(file, stored, FileAttributes.Normal);

      // a restored file may need fresh parity (it might never have had any, or parity was the problem)
      if (!this._parityStore.IsParityCurrent(file, stored))
        this._parityStore.BuildParity(file);

      return new RepairOutcome(file, RepairResult.RepairedFromMirror, badShardsFound);
    }

    /// <summary>
    /// Re-applies the recorded modification time so the healed file does not read as a fresh
    /// edit; legacy entries without a timestamp get their database entry refreshed instead.
    /// </summary>
    private void _RestoreMetadata(FileInfo file, ChecksumEntry stored, FileAttributes attributes) {
      file.Refresh();
      if (stored.ModificationTimeTicks != null)
        File.SetLastWriteTimeUtc(file.FullName, new DateTime(stored.ModificationTimeTicks.Value, DateTimeKind.Utc));
      else
        this._checker.UpdateFile(file);

      try {
        if (attributes != FileAttributes.Normal)
          file.Attributes = attributes;
      } catch (IOException) {
        ;
      } catch (UnauthorizedAccessException) {
        ;
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
