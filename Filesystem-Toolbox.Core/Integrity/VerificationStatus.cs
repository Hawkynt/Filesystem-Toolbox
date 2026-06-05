namespace Filesystem_Toolbox.Core.Integrity {

  public enum VerificationStatus {

    /// <summary>Content matches the database.</summary>
    Ok,

    /// <summary>Content changed although size and modification time did not - silent corruption.</summary>
    BitRot,

    /// <summary>Content changed together with its metadata - most likely an intentional edit.</summary>
    Modified,

    /// <summary>File exists but is not in the database yet.</summary>
    New,

    /// <summary>Database entry exists but the file is gone.</summary>
    Missing,

    /// <summary>File is fine but its parity is bound to an older content state and needs a rebuild.</summary>
    ParityStale,

    /// <summary>Verification itself failed (I/O error, access denied, ...).</summary>
    Error,

  }
}
