using System;
using System.IO;
using System.Linq;
using System.Threading;
using Filesystem_Toolbox.Core.Integrity;

namespace Filesystem_Toolbox.Core.Redundancy {

  /// <summary>
  /// Owns the parity files of one watched root: every protected file gets a sibling
  /// <c>&lt;root&gt;/.fst/parity/&lt;relative-path&gt;.par</c>. Works on FAT32/exFAT - the
  /// store consists of ordinary files; NTFS niceties (hidden/system attributes, compression)
  /// are applied opportunistically.
  /// </summary>
  public sealed class ParityStore {

    private const string _PARITY_FOLDER_NAME = "parity";
    private const string _PARITY_EXTENSION = ".par";

    private readonly ParityGeometry _geometry;

    public DirectoryInfo Root { get; }
    public DirectoryInfo ParityRoot { get; }

    public ParityStore(DirectoryInfo root, int redundancyPercent) {
      this.Root = root ?? throw new ArgumentNullException(nameof(root));
      this._geometry = ParityGeometry.FromRedundancyPercent(redundancyPercent);
      this.ParityRoot = new DirectoryInfo(Path.Combine(root.FullName, FolderIntegrityChecker.PROTECTED_FOLDER_NAME, _PARITY_FOLDER_NAME));
    }

    /// <summary>How many damaged 64 KiB regions per MiB stripe are repairable with this store's settings.</summary>
    public int ParityShardsPerStripe => this._geometry.ParityShardCount;

    public FileInfo GetParityFile(FileInfo protectedFile) {
      if (protectedFile == null) throw new ArgumentNullException(nameof(protectedFile));

      var relative = protectedFile.RelativeTo(this.Root);
      return new FileInfo(Path.Combine(this.ParityRoot.FullName, relative + _PARITY_EXTENSION));
    }

    public bool HasParity(FileInfo protectedFile) {
      var parityFile = this.GetParityFile(protectedFile);
      parityFile.Refresh();
      return parityFile.Exists;
    }

    /// <summary>
    /// (Re)builds the parity for a file and returns the SHA-512 the parity is now bound to.
    /// </summary>
    public byte[] BuildParity(FileInfo protectedFile, CancellationToken token = default) {
      var parityFile = this.GetParityFile(protectedFile);
      this._EnsureStoreDirectory(parityFile.Directory);

      var writer = new ParityFileWriter(this._geometry);
      return writer.Write(protectedFile, parityFile, token);
    }

    public void DeleteParity(FileInfo protectedFile) {
      var parityFile = this.GetParityFile(protectedFile);
      parityFile.Refresh();
      if (!parityFile.Exists)
        return;

      parityFile.Delete();
      this._PruneEmptyDirectories(parityFile.Directory);
    }

    public void MoveParity(FileInfo oldFile, FileInfo newFile) {
      var oldParity = this.GetParityFile(oldFile);
      oldParity.Refresh();
      if (!oldParity.Exists)
        return;

      var newParity = this.GetParityFile(newFile);
      this._EnsureStoreDirectory(newParity.Directory);
      newParity.Refresh();
      if (newParity.Exists)
        newParity.Delete();

      oldParity.MoveTo(newParity.FullName);
      this._PruneEmptyDirectories(oldParity.Directory);
    }

    /// <summary>
    /// Is the stored parity bound to exactly this content state? <c>false</c> also when
    /// there is no parity at all or its header is unreadable - in every case a rebuild is due.
    /// </summary>
    public bool IsParityCurrent(FileInfo protectedFile, ChecksumEntry entry) {
      var parityFile = this.GetParityFile(protectedFile);
      parityFile.Refresh();
      if (!parityFile.Exists)
        return false;

      try {
        using var reader = ParityFileReader.Open(parityFile);
        return reader.Header.OriginalSha512.SequenceEqual(entry.Hash);
      } catch (ParityFormatException) {
        return false;
      } catch (FormatException) {
        return false;
      } catch (IOException) {
        return false;
      }
    }

    internal ParityFileReader OpenParity(FileInfo protectedFile) => ParityFileReader.Open(this.GetParityFile(protectedFile));

    internal ParityGeometry Geometry => this._geometry;

    private void _EnsureStoreDirectory(DirectoryInfo parityDirectory) {
      var protectedFolder = new DirectoryInfo(Path.Combine(this.Root.FullName, FolderIntegrityChecker.PROTECTED_FOLDER_NAME));
      var existedBefore = protectedFolder.Exists;

      parityDirectory.Create();

      if (existedBefore)
        return;

      // hide the store from the user's everyday view; silently no-ops on FAT
      try {
        protectedFolder.Refresh();
        protectedFolder.Attributes |= FileAttributes.Hidden | FileAttributes.System;
      } catch (IOException) {
        ;
      } catch (UnauthorizedAccessException) {
        ;
      }
    }

    private void _PruneEmptyDirectories(DirectoryInfo directory) {
      try {
        while (directory != null
               && directory.FullName.Length > this.ParityRoot.FullName.Length
               && directory.Exists
               && !directory.EnumerateFileSystemInfos().Any()) {
          directory.Delete();
          directory = directory.Parent;
        }
      } catch (IOException) {
        ;
      } catch (UnauthorizedAccessException) {
        ;
      }
    }

  }
}
