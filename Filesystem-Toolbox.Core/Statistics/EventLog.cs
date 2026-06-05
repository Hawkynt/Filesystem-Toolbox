using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Filesystem_Toolbox.Core.Statistics {

  /// <summary>
  /// Append-only JSONL event history: one compact JSON object per line, thread-safe appends,
  /// rolled to a single predecessor segment (<c>*.1.jsonl</c>) once the active file exceeds
  /// 5 MB. Reading tolerates malformed lines (e.g. a torn final write).
  /// </summary>
  public sealed class EventLog {

    public const long ROLL_THRESHOLD_BYTES = 5L * 1024 * 1024;

    private static readonly JsonSerializerOptions _OPTIONS = new JsonSerializerOptions {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      PropertyNameCaseInsensitive = true,
      DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly FileInfo _file;
    private readonly object _gate = new object();

    public EventLog(FileInfo file) => this._file = file ?? throw new ArgumentNullException(nameof(file));

    private FileInfo _RolledFile => new FileInfo(Path.ChangeExtension(this._file.FullName, ".1.jsonl"));

    public void Append(EventRecord record) {
      if (record == null) throw new ArgumentNullException(nameof(record));

      lock (this._gate) {
        this._RollIfNeeded();
        File.AppendAllText(this._file.FullName, JsonSerializer.Serialize(record, _OPTIONS) + Environment.NewLine);
      }
    }

    public IEnumerable<EventRecord> ReadAll() {
      var result = new List<EventRecord>();
      lock (this._gate) {
        this._ReadInto(this._RolledFile, result);
        this._ReadInto(this._file, result);
      }

      return result;
    }

    private void _ReadInto(FileInfo file, List<EventRecord> target) {
      file.Refresh();
      if (!file.Exists)
        return;

      foreach (var line in File.ReadAllLines(file.FullName)) {
        if (string.IsNullOrWhiteSpace(line))
          continue;

        try {
          var record = JsonSerializer.Deserialize<EventRecord>(line, _OPTIONS);
          if (record != null)
            target.Add(record);
        } catch (JsonException) {
          ; // a torn line must never break statistics
        }
      }
    }

    private void _RollIfNeeded() {
      this._file.Refresh();
      if (!this._file.Exists || this._file.Length < ROLL_THRESHOLD_BYTES)
        return;

      var rolled = this._RolledFile;
      rolled.Refresh();
      if (rolled.Exists)
        rolled.Delete();

      File.Move(this._file.FullName, rolled.FullName);
    }

  }
}
