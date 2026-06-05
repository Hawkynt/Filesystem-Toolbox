using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Filesystem_Toolbox.Core.Redundancy;

namespace Filesystem_Toolbox.Core.Integrity {

  /// <summary>
  /// Classifies files against the checksum database. The crucial distinction for the
  /// USB-stick use case: a hash mismatch with unchanged size and modification time is
  /// <see cref="VerificationStatus.BitRot"/> (nothing legitimately wrote the file - the
  /// medium silently lost data), while a mismatch with changed metadata is just
  /// <see cref="VerificationStatus.Modified"/> and must never be "repaired" backwards.
  /// </summary>
  public sealed class IntegrityVerifier {

    private readonly FolderIntegrityChecker _checker;
    private readonly ParityStore _parityStore;

    public IntegrityVerifier(FolderIntegrityChecker checker, ParityStore parityStore = null) {
      this._checker = checker ?? throw new ArgumentNullException(nameof(checker));
      this._parityStore = parityStore;
    }

    /// <summary>Classifies a single file against its (optional) stored entry.</summary>
    public VerificationResult Classify(FileInfo file, ChecksumEntry? storedEntry) {
      if (file == null) throw new ArgumentNullException(nameof(file));

      file.Refresh();
      if (!file.Exists)
        return new VerificationResult(file, storedEntry == null ? VerificationStatus.Error : VerificationStatus.Missing, storedEntry, null);

      ChecksumEntry actual;
      try {
        actual = ChecksumEntry.FromFile(file);
      } catch (Exception exception) {
        return new VerificationResult(file, VerificationStatus.Error, storedEntry, null, exception);
      }

      if (storedEntry == null)
        return new VerificationResult(file, VerificationStatus.New, null, actual);

      var stored = storedEntry.Value;
      if (actual.ContentEquals(stored)) {
        if (this._parityStore != null && !this._parityStore.IsParityCurrent(file, stored))
          return new VerificationResult(file, VerificationStatus.ParityStale, stored, actual);

        return new VerificationResult(file, VerificationStatus.Ok, stored, actual);
      }

      return new VerificationResult(
        file,
        actual.MetadataEquals(stored) ? VerificationStatus.BitRot : VerificationStatus.Modified,
        stored,
        actual
      );
    }

    /// <summary>
    /// Verifies every tracked file plus everything on disk that is not tracked yet.
    /// Only non-<see cref="VerificationStatus.Ok"/> results are reported.
    /// </summary>
    public IEnumerable<VerificationResult> VerifyAll(CancellationToken token = default) {
      var database = this._checker.GetDatabaseSnapshot();

      foreach (var pair in database) {
        token.ThrowIfCancellationRequested();

        var file = this._checker.GetFile(pair.Key);
        ChecksumEntry? stored = ChecksumEntry.TryParse(pair.Value, out var entry) ? entry : (ChecksumEntry?)null;
        var result = this.Classify(file, stored);
        if (result.Status != VerificationStatus.Ok)
          yield return result;
      }

      foreach (var result in this._VerifyUntracked(database, token))
        yield return result;
    }

    private IEnumerable<VerificationResult> _VerifyUntracked(Dictionary<string, string> database, CancellationToken token) {
      var root = this._checker.RootDirectory;
      var protectedPrefix = Path.Combine(root.FullName, FolderIntegrityChecker.PROTECTED_FOLDER_NAME) + Path.DirectorySeparatorChar;

      foreach (var file in root.EnumerateFiles("*", SearchOption.AllDirectories)) {
        token.ThrowIfCancellationRequested();

        if (file.FullName.StartsWith(protectedPrefix, StringComparison.OrdinalIgnoreCase))
          continue;

        var key = file.RelativeTo(root);
        if (string.Equals(key, "checksum.db", StringComparison.OrdinalIgnoreCase))
          continue;

        if (database.ContainsKey(key))
          continue;

        yield return this.Classify(file, null);
      }
    }

  }
}
