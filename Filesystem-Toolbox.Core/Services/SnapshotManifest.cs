using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Filesystem_Toolbox.Core.Integrity;

namespace Filesystem_Toolbox.Core.Services {

  /// <summary>
  /// The per-snapshot manifest (<c>.fst-snapshot.manifest</c>): one <see cref="ChecksumEntry"/>
  /// line per backed-up file, same <c>"&lt;value&gt; =&gt; &lt;relpath&gt;"</c> format as
  /// checksum.db - restores and dedup decisions never have to re-hash the backup.
  /// </summary>
  internal sealed class SnapshotManifest {

    public const string FILE_NAME = ".fst-snapshot.manifest";

    private readonly Dictionary<string, ChecksumEntry> _entries;

    public IReadOnlyDictionary<string, ChecksumEntry> Entries => this._entries;

    public SnapshotManifest() : this(new Dictionary<string, ChecksumEntry>(StringComparer.OrdinalIgnoreCase)) { }

    private SnapshotManifest(Dictionary<string, ChecksumEntry> entries) => this._entries = entries;

    public bool TryGet(string relativePath, out ChecksumEntry entry) => this._entries.TryGetValue(relativePath, out entry);

    public void Set(string relativePath, ChecksumEntry entry) => this._entries[relativePath] = entry;

    /// <summary>Loads a snapshot's manifest; missing or unreadable yields an empty manifest (the snapshot is then useless for dedup/restore but harmless).</summary>
    public static SnapshotManifest Load(DirectoryInfo snapshotDirectory) {
      var result = new Dictionary<string, ChecksumEntry>(StringComparer.OrdinalIgnoreCase);
      var file = new FileInfo(Path.Combine(snapshotDirectory.FullName, FILE_NAME));
      file.Refresh();
      if (!file.Exists)
        return new SnapshotManifest(result);

      foreach (var line in File.ReadAllLines(file.FullName)) {
        var index = line.IndexOf("=>", StringComparison.Ordinal);
        if (index < 1)
          continue;

        if (ChecksumEntry.TryParse(line.Substring(0, index).TrimEnd(), out var entry))
          result[line.Substring(index + 2).TrimStart()] = entry;
      }

      return new SnapshotManifest(result);
    }

    public void Save(DirectoryInfo snapshotDirectory) {
      var file = new FileInfo(Path.Combine(snapshotDirectory.FullName, FILE_NAME));
      File.WriteAllLines(
        file.FullName,
        this._entries.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase).Select(p => $"{p.Value} => {p.Key}")
      );

      try {
        file.Attributes |= FileAttributes.Hidden;
      } catch (IOException) {
        ;
      } catch (UnauthorizedAccessException) {
        ;
      }
    }

  }
}
