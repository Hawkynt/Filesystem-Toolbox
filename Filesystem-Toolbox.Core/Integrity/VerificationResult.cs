using System;
using System.IO;

namespace Filesystem_Toolbox.Core.Integrity {

  /// <summary>The classified outcome of verifying one file against the checksum database.</summary>
  public sealed class VerificationResult {

    public FileInfo File { get; }
    public VerificationStatus Status { get; }
    public ChecksumEntry? StoredEntry { get; }
    public ChecksumEntry? ActualEntry { get; }
    public Exception Error { get; }

    public VerificationResult(FileInfo file, VerificationStatus status, ChecksumEntry? storedEntry, ChecksumEntry? actualEntry, Exception error = null) {
      this.File = file ?? throw new ArgumentNullException(nameof(file));
      this.Status = status;
      this.StoredEntry = storedEntry;
      this.ActualEntry = actualEntry;
      this.Error = error;
    }

  }
}
