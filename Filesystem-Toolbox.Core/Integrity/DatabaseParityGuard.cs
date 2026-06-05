using System;
using System.IO;
using System.Linq;
using System.Threading;
using Filesystem_Toolbox.Core.Redundancy;

namespace Filesystem_Toolbox.Core.Integrity {

  public enum DbHealResult {

    /// <summary>The database matches its parity - nothing to do.</summary>
    NotNeeded,

    /// <summary>No (usable) parity exists - nothing to verify against.</summary>
    NoParity,

    /// <summary>The database was corrupted and has been restored from its parity.</summary>
    Repaired,

    /// <summary>The database is corrupted beyond what its parity can fix - tolerant parsing salvages what it can.</summary>
    Unrepairable,

    /// <summary>The database was legitimately written after its parity - the parity was rebound, nothing repaired.</summary>
    ParityRebuilt,

  }

  /// <summary>
  /// Protects the checksum database itself with the same Reed-Solomon machinery that guards
  /// the user's files. There is no chicken-and-egg problem: the parity header carries the
  /// expected SHA-512 of the database (and the header's own CRC proves the header), so a
  /// rotten database can be detected and stripe-repaired without any external record.
  /// </summary>
  internal sealed class DatabaseParityGuard {

    private const string _DBPARITY_FOLDER_NAME = "dbparity";
    private const string _PARITY_EXTENSION = ".par";
    private const int _REDUNDANCY_PERCENT = 25;

    private readonly DirectoryInfo _root;

    public DatabaseParityGuard(DirectoryInfo root) => this._root = root ?? throw new ArgumentNullException(nameof(root));

    public FileInfo GetParityFile(FileInfo databaseFile) {
      if (databaseFile == null) throw new ArgumentNullException(nameof(databaseFile));

      return new FileInfo(Path.Combine(
        this._root.FullName,
        FolderIntegrityChecker.PROTECTED_FOLDER_NAME,
        _DBPARITY_FOLDER_NAME,
        databaseFile.Name + _PARITY_EXTENSION
      ));
    }

    /// <summary>(Re)builds the parity for the database file.</summary>
    public void Protect(FileInfo databaseFile, CancellationToken token = default) {
      var parityFile = this.GetParityFile(databaseFile);
      parityFile.Directory.Create();
      new ParityFileWriter(ParityGeometry.FromRedundancyPercent(_REDUNDANCY_PERCENT)).Write(databaseFile, parityFile, token);
    }

    /// <summary>
    /// Does the database match the hash its parity is bound to? <c>true</c> also when there is
    /// no (readable) parity - without a trustworthy reference there is nothing to cry wolf about.
    /// </summary>
    public bool IsHealthy(FileInfo databaseFile) {
      databaseFile.Refresh();
      if (!databaseFile.Exists)
        return true;

      var parityFile = this.GetParityFile(databaseFile);
      parityFile.Refresh();
      if (!parityFile.Exists)
        return true;

      try {
        using (var reader = ParityFileReader.Open(parityFile))
          return databaseFile.ComputeSHA512Hash().SequenceEqual(reader.Header.OriginalSha512);
      } catch (ParityFormatException) {
        return true;
      } catch (IOException) {
        return true;
      } catch (UnauthorizedAccessException) {
        return true;
      }
    }

    /// <summary>Repairs a rotten database in place from its parity (header-trusted hash).</summary>
    public DbHealResult Heal(FileInfo databaseFile, CancellationToken token = default) {
      if (this.IsHealthy(databaseFile))
        return DbHealResult.NotNeeded;

      var parityFile = this.GetParityFile(databaseFile);

      // the bit-rot signature, applied to the database itself: a write AFTER the last parity
      // build means the parity is stale (e.g. shutdown beat the debounced rebuild) - healing
      // would regress a newer good database, so rebind the parity instead
      databaseFile.Refresh();
      parityFile.Refresh();
      if (databaseFile.LastWriteTimeUtc > parityFile.LastWriteTimeUtc) {
        try {
          this.Protect(databaseFile, token);
          return DbHealResult.ParityRebuilt;
        } catch (IOException) {
          return DbHealResult.NoParity;
        } catch (UnauthorizedAccessException) {
          return DbHealResult.NoParity;
        }
      }

      try {
        var outcome = ParityRepairCore.TryRepairFile(databaseFile, parityFile, expectedHash: null, token);
        return outcome.Repaired ? DbHealResult.Repaired : DbHealResult.Unrepairable;
      } catch (ParityFormatException) {
        return DbHealResult.NoParity;
      } catch (IOException) {
        return DbHealResult.Unrepairable;
      } catch (UnauthorizedAccessException) {
        return DbHealResult.Unrepairable;
      }
    }

  }
}
