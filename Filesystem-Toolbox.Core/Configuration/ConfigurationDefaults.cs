using System;
using Filesystem_Toolbox.Core.Scheduling;

namespace Filesystem_Toolbox.Core.Configuration {

  /// <summary>
  /// The hard-coded floor of the settings-inheritance chain: whatever no configured folder
  /// (and no global setting) decides ends up here.
  /// </summary>
  public static class ConfigurationDefaults {

    public const int PARITY_REDUNDANCY_PERCENT = 25;
    public const bool AUTO_REPAIR = false;
    public const int REFRESH_INTERVAL_DAYS = 180;
    public const bool DEDUP_ENABLED = false;
    public const int GFS_KEEP_DAILY = 7;
    public const int GFS_KEEP_WEEKLY = 4;
    public const int GFS_KEEP_MONTHLY = 12;
    public const int DEGRADATION_WARNING_ERRORS_PER_MONTH = 5;
    public const bool TOAST_NOTIFICATIONS = true;

    public static readonly ScheduleSpec VERIFY_SCHEDULE = ScheduleSpec.Every(TimeSpan.FromMinutes(10));

  }
}
