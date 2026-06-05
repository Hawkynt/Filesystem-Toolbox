using System.Collections.Generic;
using Filesystem_Toolbox.Core.Scheduling;

namespace Filesystem_Toolbox.Core.Configuration {

  /// <summary>
  /// Root of the persisted application configuration (FilesystemToolbox.json), schema v2:
  /// folder entries carry nullable settings that inherit along the path; the global
  /// verify schedule sits between the folder chain and the hard-coded defaults.
  /// </summary>
  public sealed class ToolboxConfiguration {

    public const int CURRENT_SCHEMA_VERSION = 2;

    public int SchemaVersion { get; set; } = CURRENT_SCHEMA_VERSION;

    /// <summary>The global default verify schedule; null falls back to <see cref="ConfigurationDefaults.VERIFY_SCHEDULE"/>.</summary>
    public ScheduleSpec? VerifySchedule { get; set; }

    public List<WatchedFolderConfiguration> Folders { get; set; } = new List<WatchedFolderConfiguration>();

  }
}
