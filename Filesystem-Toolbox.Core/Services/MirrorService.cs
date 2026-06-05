using System;
using System.IO;
using System.Linq;
using System.Threading;
using Filesystem_Toolbox.Core.Integrity;

namespace Filesystem_Toolbox.Core.Services {

  /// <summary>
  /// Whole-file fallback when parity cannot help (file deleted, too many damaged regions):
  /// a mirror directory holds plain copies under the same relative paths. Restoring is
  /// hash-gated - a mirror copy is only used when its content matches the recorded checksum,
  /// so rot in the mirror can never overwrite the original with different garbage.
  /// </summary>
  public sealed class MirrorService {

    public DirectoryInfo Root { get; }
    public DirectoryInfo MirrorRoot { get; }

    public MirrorService(DirectoryInfo root, DirectoryInfo mirrorRoot) {
      this.Root = root ?? throw new ArgumentNullException(nameof(root));
      this.MirrorRoot = mirrorRoot ?? throw new ArgumentNullException(nameof(mirrorRoot));
    }

    public FileInfo GetMirrorFile(FileInfo file) {
      if (file == null) throw new ArgumentNullException(nameof(file));

      return new FileInfo(Path.Combine(this.MirrorRoot.FullName, file.RelativeTo(this.Root)));
    }

    public bool TryGetMirror(FileInfo file, out FileInfo mirrorFile) {
      mirrorFile = this.GetMirrorFile(file);
      mirrorFile.Refresh();
      return mirrorFile.Exists;
    }

    /// <summary>
    /// Restores <paramref name="file"/> from its mirror copy if - and only if - the mirror's
    /// SHA-512 equals <paramref name="expectedHash"/>.
    /// </summary>
    public bool Restore(FileInfo file, byte[] expectedHash, CancellationToken token = default) {
      if (expectedHash == null) throw new ArgumentNullException(nameof(expectedHash));

      if (!this.TryGetMirror(file, out var mirrorFile))
        return false;

      token.ThrowIfCancellationRequested();
      if (!mirrorFile.ComputeSHA512Hash().SequenceEqual(expectedHash))
        return false;

      var temporary = new FileInfo(file.FullName + ".fst-restore");
      try {
        mirrorFile.CopyTo(temporary.FullName, true);

        file.Refresh();
        if (file.Exists)
          file.Delete();

        File.Move(temporary.FullName, file.FullName);
        return true;
      } finally {
        temporary.Refresh();
        if (temporary.Exists)
          temporary.Delete();
      }
    }

    /// <summary>Pushes the current (verified-good) content of a file into the mirror.</summary>
    public void Sync(FileInfo file, CancellationToken token = default) {
      if (file == null) throw new ArgumentNullException(nameof(file));

      token.ThrowIfCancellationRequested();
      var mirrorFile = this.GetMirrorFile(file);
      mirrorFile.Directory.Create();
      file.CopyTo(mirrorFile.FullName, true);
    }

  }
}
