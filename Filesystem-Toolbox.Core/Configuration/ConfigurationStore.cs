using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Filesystem_Toolbox.Core.Scheduling;

namespace Filesystem_Toolbox.Core.Configuration {

  /// <summary>
  /// Loads and saves <see cref="ToolboxConfiguration"/>. Older formats are upgraded on first
  /// contact: schema v1 (flat non-null settings, mirrorPath, checkIntervalMinutes) becomes a
  /// fully-explicit v2 (the v1 values WERE the effective values, so nothing changes behavior;
  /// mirrorPath turns into backupPath, the interval into the global verify schedule), and the
  /// legacy CheckedFolders.lst (one path per line) becomes inherit-everything v2 entries.
  /// </summary>
  public static class ConfigurationStore {

    #region v1 shape

    private sealed class _V1Configuration {
      public int SchemaVersion { get; set; }
      public int CheckIntervalMinutes { get; set; } = 10;
      public List<_V1Folder> Folders { get; set; } = new List<_V1Folder>();
    }

    private sealed class _V1Folder {
      public string Path { get; set; }
      public int ParityRedundancyPercent { get; set; } = 25;
      public bool AutoRepair { get; set; }
      public string MirrorPath { get; set; }
      public int RefreshIntervalDays { get; set; } = 180;
      public string OnCorruptionCommand { get; set; }
      public bool DedupEnabled { get; set; }
    }

    #endregion

    private static readonly JsonSerializerOptions _OPTIONS = new JsonSerializerOptions {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      PropertyNameCaseInsensitive = true,
      WriteIndented = true,
      DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
      AllowTrailingCommas = true,
      ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Loads the configuration, upgrading older formats in place (the original v1 file is kept
    /// as *.v1.bak, a legacy list file as *.bak).
    /// </summary>
    /// <exception cref="JsonException">The JSON file exists but is malformed.</exception>
    public static ToolboxConfiguration Load(FileInfo jsonFile, FileInfo legacyListFile = null) {
      if (jsonFile == null) throw new ArgumentNullException(nameof(jsonFile));

      jsonFile.Refresh();
      if (jsonFile.Exists) {
        var json = File.ReadAllText(jsonFile.FullName);
        if (_ReadSchemaVersion(json) >= ToolboxConfiguration.CURRENT_SCHEMA_VERSION)
          return JsonSerializer.Deserialize<ToolboxConfiguration>(json, _OPTIONS) ?? new ToolboxConfiguration();

        var migrated = _MigrateFromV1(JsonSerializer.Deserialize<_V1Configuration>(json, _OPTIONS) ?? new _V1Configuration());
        File.Copy(jsonFile.FullName, jsonFile.FullName + ".v1.bak", true);
        Save(migrated, jsonFile);
        return migrated;
      }

      var result = new ToolboxConfiguration();
      if (legacyListFile == null)
        return result;

      legacyListFile.Refresh();
      if (!legacyListFile.Exists)
        return result;

      result.Folders.AddRange(
        from line in File.ReadAllLines(legacyListFile.FullName)
        where !string.IsNullOrWhiteSpace(line)
        select new WatchedFolderConfiguration { Path = line.Trim() }
      );

      Save(result, jsonFile);
      _RenameToBackup(legacyListFile);
      return result;
    }

    public static void Save(ToolboxConfiguration configuration, FileInfo jsonFile) {
      if (configuration == null) throw new ArgumentNullException(nameof(configuration));
      if (jsonFile == null) throw new ArgumentNullException(nameof(jsonFile));

      File.WriteAllText(jsonFile.FullName, JsonSerializer.Serialize(configuration, _OPTIONS));
    }

    private static int _ReadSchemaVersion(string json) {
      using (var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }))
        return document.RootElement.TryGetProperty("schemaVersion", out var property) && property.TryGetInt32(out var version)
          ? version
          : 1;
    }

    /// <summary>
    /// v1 values were effective values, so every field becomes an explicit override - the
    /// upgraded configuration behaves exactly like before.
    /// </summary>
    private static ToolboxConfiguration _MigrateFromV1(_V1Configuration v1) => new ToolboxConfiguration {
      VerifySchedule = ScheduleSpec.Every(TimeSpan.FromMinutes(Math.Max(1, v1.CheckIntervalMinutes))),
      Folders = v1.Folders.Where(f => !f.Path.IsNullOrWhiteSpace()).Select(f => new WatchedFolderConfiguration {
        Path = f.Path,
        ParityRedundancyPercent = f.ParityRedundancyPercent,
        AutoRepair = f.AutoRepair,
        RefreshIntervalDays = f.RefreshIntervalDays,
        OnCorruptionCommand = f.OnCorruptionCommand.IsNullOrWhiteSpace() ? null : f.OnCorruptionCommand,
        DedupEnabled = f.DedupEnabled,
        BackupPath = f.MirrorPath.IsNullOrWhiteSpace() ? null : f.MirrorPath,
      }).ToList(),
    };

    private static void _RenameToBackup(FileInfo file) {
      var backup = new FileInfo(file.FullName + ".bak");
      if (backup.Exists)
        backup.Delete();

      file.MoveTo(backup.FullName);
    }

  }
}
