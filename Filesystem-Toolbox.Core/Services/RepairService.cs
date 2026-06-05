using System;
using System.IO;
using System.Linq;
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

      ParityRepairCore.Outcome outcome;

      // NOTE: the mirror fallback and the parity self-heal both rewrite the parity file,
      //       so they must only run after the repair core released its reader handle
      try {
        outcome = ParityRepairCore.TryRepairFile(file, this._parityStore.GetParityFile(file), stored.Hash, token);
      } catch (ParityFormatException) {

        // structurally broken parity - useless for this repair, but rebuildable once the file is healthy again
        outcome = default;
      }

      if (!outcome.Repaired)
        return this._TryMirrorRestore(file, stored, outcome.BadShards)
               ?? new RepairOutcome(file, RepairResult.Unrepairable, outcome.BadShards, outcome.StripesRepaired);

      this._RestoreMetadata(file, stored, FileAttributes.Normal);

      // parity self-heal: data is known-good again, so damaged parity shards can be regenerated
      if (outcome.ParityWasDamaged)
        this._parityStore.BuildParity(file, token);

      return new RepairOutcome(file, RepairResult.Repaired, outcome.BadShards, outcome.StripesRepaired);
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

  }
}
