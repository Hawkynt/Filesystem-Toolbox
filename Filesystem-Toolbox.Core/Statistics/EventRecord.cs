using System;
using System.Text.Json.Serialization;

namespace Filesystem_Toolbox.Core.Statistics {

  [JsonConverter(typeof(JsonStringEnumConverter))]
  public enum EventType {
    VerifyRun,
    BitRotFound,
    Repaired,
    RepairedFromBackup,
    Unrepairable,
    Refreshed,
    BackupRun,
    DbRepaired,
    DeviceWarning,
  }

  /// <summary>One line of the append-only event history (events.jsonl).</summary>
  public sealed class EventRecord {

    public DateTime Utc { get; set; }
    public string Root { get; set; }
    public EventType Type { get; set; }

    /// <summary>Affected file (relative or absolute), where applicable.</summary>
    public string Path { get; set; }

    /// <summary>Free-form detail, e.g. per-status counts of a verify run ("BitRot=2;Modified=1").</summary>
    public string Detail { get; set; }

    public int? FilesChecked { get; set; }
    public int? Problems { get; set; }
    public int? Count { get; set; }
    public int? Linked { get; set; }
    public int? Copied { get; set; }

    public static EventRecord Now(string root, EventType type, string path = null, string detail = null)
      => new EventRecord { Utc = DateTime.UtcNow, Root = root, Type = type, Path = path, Detail = detail };

  }
}
