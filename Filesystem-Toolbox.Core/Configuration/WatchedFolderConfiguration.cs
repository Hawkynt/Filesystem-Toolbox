namespace Filesystem_Toolbox.Core.Configuration {

  /// <summary>
  /// Per-folder policy: what to watch and how to protect, repair and maintain it.
  /// </summary>
  public sealed class WatchedFolderConfiguration {

    /// <summary>The absolute path of the folder tree to watch.</summary>
    public string Path { get; set; }

    /// <summary>How much Reed-Solomon parity to keep, in percent of the protected data (0 disables parity).</summary>
    public int ParityRedundancyPercent { get; set; } = 25;

    /// <summary>Whether detected bit rot is repaired automatically without asking.</summary>
    public bool AutoRepair { get; set; }

    /// <summary>Optional root of a mirror copy used to restore files parity cannot save.</summary>
    public string MirrorPath { get; set; }

    /// <summary>Days between preventive rewrites of verified-good files to recharge flash cells (0 disables refresh).</summary>
    public int RefreshIntervalDays { get; set; } = 180;

    /// <summary>Optional command executed when corruption is found; {file} and {folder} are replaced.</summary>
    public string OnCorruptionCommand { get; set; }

    /// <summary>Whether the duplicate-to-hardlink merger may process this folder.</summary>
    public bool DedupEnabled { get; set; }

  }
}
