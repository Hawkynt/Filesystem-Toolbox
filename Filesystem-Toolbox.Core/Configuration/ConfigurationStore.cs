using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Filesystem_Toolbox.Core.Configuration {

  /// <summary>
  /// Loads and saves <see cref="ToolboxConfiguration"/>, migrating the legacy
  /// CheckedFolders.lst (one folder path per line) on first contact.
  /// </summary>
  public static class ConfigurationStore {

    private static readonly JsonSerializerOptions _OPTIONS = new JsonSerializerOptions {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      PropertyNameCaseInsensitive = true,
      WriteIndented = true,
      DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
      AllowTrailingCommas = true,
      ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Loads the configuration from <paramref name="jsonFile"/>. When that file does not exist yet but a
    /// legacy <paramref name="legacyListFile"/> does, its folder list is migrated (with default policies),
    /// the result is saved and the legacy file is renamed to *.bak. Without either, defaults are returned.
    /// </summary>
    /// <exception cref="JsonException">The JSON file exists but is malformed.</exception>
    public static ToolboxConfiguration Load(FileInfo jsonFile, FileInfo legacyListFile = null) {
      if (jsonFile == null) throw new ArgumentNullException(nameof(jsonFile));

      jsonFile.Refresh();
      if (jsonFile.Exists)
        return JsonSerializer.Deserialize<ToolboxConfiguration>(File.ReadAllText(jsonFile.FullName), _OPTIONS) ?? new ToolboxConfiguration();

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

    private static void _RenameToBackup(FileInfo file) {
      var backup = new FileInfo(file.FullName + ".bak");
      if (backup.Exists)
        backup.Delete();

      file.MoveTo(backup.FullName);
    }

  }
}
