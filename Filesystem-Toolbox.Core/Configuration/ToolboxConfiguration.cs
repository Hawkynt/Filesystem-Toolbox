using System.Collections.Generic;

namespace Filesystem_Toolbox.Core.Configuration {

  /// <summary>
  /// Root of the persisted application configuration (FilesystemToolbox.json).
  /// </summary>
  public sealed class ToolboxConfiguration {

    public const int CURRENT_SCHEMA_VERSION = 1;

    public int SchemaVersion { get; set; } = CURRENT_SCHEMA_VERSION;

    /// <summary>Minutes between automatic integrity verification runs.</summary>
    public int CheckIntervalMinutes { get; set; } = 10;

    public List<WatchedFolderConfiguration> Folders { get; set; } = new List<WatchedFolderConfiguration>();

  }
}
