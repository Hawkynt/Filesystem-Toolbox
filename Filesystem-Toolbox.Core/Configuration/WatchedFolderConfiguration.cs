using Filesystem_Toolbox.Core.Scheduling;

namespace Filesystem_Toolbox.Core.Configuration {

  /// <summary>
  /// A (possibly partial) policy attached to one folder path. Every field is nullable:
  /// <c>null</c> (or an empty string) means "inherit from the nearest configured ancestor,
  /// the global configuration, or the hard-coded defaults". Top-level entries - those not
  /// nested inside another configured path - are the watch roots; nested entries only
  /// override settings for their subtree. Removing an entry or nulling a field restores
  /// the inheritance chain.
  /// </summary>
  public sealed class WatchedFolderConfiguration {

    /// <summary>The absolute folder path this entry applies to.</summary>
    public string Path { get; set; }

    /// <summary>Reed-Solomon parity to keep, percent of protected data (0 disables parity for the subtree).</summary>
    public int? ParityRedundancyPercent { get; set; }

    /// <summary>Whether detected bit rot is repaired automatically without asking.</summary>
    public bool? AutoRepair { get; set; }

    /// <summary>Days between preventive flash refreshes (0 disables refresh).</summary>
    public int? RefreshIntervalDays { get; set; }

    /// <summary>Command executed when corruption is found; {file} and {folder} are replaced.</summary>
    public string OnCorruptionCommand { get; set; }

    /// <summary>Whether the duplicate-to-hardlink merger may process this subtree.</summary>
    public bool? DedupEnabled { get; set; }

    /// <summary>How often this folder is verified.</summary>
    public ScheduleSpec? VerifySchedule { get; set; }

    /// <summary>Root of the versioned GFS backup store for this folder.</summary>
    public string BackupPath { get; set; }

    /// <summary>When backups run automatically; null = manual backups only.</summary>
    public ScheduleSpec? BackupSchedule { get; set; }

    /// <summary>GFS retention: how many daily snapshots to keep.</summary>
    public int? GfsKeepDaily { get; set; }

    /// <summary>GFS retention: how many weekly snapshots to keep.</summary>
    public int? GfsKeepWeekly { get; set; }

    /// <summary>GFS retention: how many monthly snapshots to keep.</summary>
    public int? GfsKeepMonthly { get; set; }

    /// <summary>Errors per month after which the medium counts as degrading (warning raised).</summary>
    public int? DegradationWarningErrorsPerMonth { get; set; }

    /// <summary>Whether balloon notifications are shown for this folder's findings.</summary>
    public bool? ToastNotifications { get; set; }

  }
}
