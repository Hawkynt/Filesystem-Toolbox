using Filesystem_Toolbox.Core.Scheduling;

namespace Filesystem_Toolbox.Core.Configuration {

  /// <summary>
  /// The fully resolved settings for one path: every inheritable field collapsed through the
  /// folder chain, the global configuration and the hard-coded defaults.
  /// </summary>
  public sealed class EffectiveSettings {

    public int ParityRedundancyPercent { get; }
    public bool AutoRepair { get; }
    public int RefreshIntervalDays { get; }

    /// <summary>May be null - no command configured anywhere along the chain.</summary>
    public string OnCorruptionCommand { get; }

    public bool DedupEnabled { get; }
    public ScheduleSpec VerifySchedule { get; }

    /// <summary>May be null - no backup configured anywhere along the chain.</summary>
    public string BackupPath { get; }

    /// <summary>May be null - a backup target may legitimately be unscheduled (manual only).</summary>
    public ScheduleSpec? BackupSchedule { get; }

    public int GfsKeepDaily { get; }
    public int GfsKeepWeekly { get; }
    public int GfsKeepMonthly { get; }
    public int DegradationWarningErrorsPerMonth { get; }
    public bool ToastNotifications { get; }

    public EffectiveSettings(
      int parityRedundancyPercent,
      bool autoRepair,
      int refreshIntervalDays,
      string onCorruptionCommand,
      bool dedupEnabled,
      ScheduleSpec verifySchedule,
      string backupPath,
      ScheduleSpec? backupSchedule,
      int gfsKeepDaily,
      int gfsKeepWeekly,
      int gfsKeepMonthly,
      int degradationWarningErrorsPerMonth,
      bool toastNotifications
    ) {
      this.ParityRedundancyPercent = parityRedundancyPercent;
      this.AutoRepair = autoRepair;
      this.RefreshIntervalDays = refreshIntervalDays;
      this.OnCorruptionCommand = onCorruptionCommand;
      this.DedupEnabled = dedupEnabled;
      this.VerifySchedule = verifySchedule;
      this.BackupPath = backupPath;
      this.BackupSchedule = backupSchedule;
      this.GfsKeepDaily = gfsKeepDaily;
      this.GfsKeepWeekly = gfsKeepWeekly;
      this.GfsKeepMonthly = gfsKeepMonthly;
      this.DegradationWarningErrorsPerMonth = degradationWarningErrorsPerMonth;
      this.ToastNotifications = toastNotifications;
    }

  }
}
