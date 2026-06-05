using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Filesystem_Toolbox.Core.Configuration;

namespace Filesystem_Toolbox.Core.Scheduling {

  public enum ScheduledAction { Verify, Backup, Refresh }

  public readonly struct DueAction {

    public string RootPath { get; }
    public ScheduledAction Action { get; }

    public DueAction(string rootPath, ScheduledAction action) {
      this.RootPath = rootPath;
      this.Action = action;
    }

    public override string ToString() => $"{this.Action} {this.RootPath}";

  }

  /// <summary>Persisted last-run state (SchedulerState.json in the application folder).</summary>
  public sealed class SchedulerState {

    public int SchemaVersion { get; set; } = 1;

    /// <summary>Last successful completion (UTC), keyed "&lt;root&gt;|&lt;action&gt;".</summary>
    public Dictionary<string, DateTime> LastRuns { get; set; } = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

  }

  /// <summary>
  /// Decides which verify/backup/refresh runs are due per watch root, based on the resolved
  /// schedules and a persisted last-run state. The decision is a pure read; actually running
  /// an action is bracketed by <see cref="TryBeginRun"/> (claims the slot - never two
  /// concurrent runs of the same action on the same root) and <see cref="CompleteRun"/>
  /// (persists the timestamp) or <see cref="AbortRun"/> (releases without recording, so a
  /// failed run stays due and is retried). Missed windows - the application was off - are
  /// detected on the next poll and collapse into a single catch-up run.
  /// </summary>
  public sealed class SchedulerService {

    private static readonly JsonSerializerOptions _OPTIONS = new JsonSerializerOptions {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      PropertyNameCaseInsensitive = true,
      WriteIndented = true,
    };

    private readonly FileInfo _stateFile;
    private readonly Func<DateTime> _now;
    private readonly object _gate = new object();
    private readonly HashSet<string> _running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private SchedulerState _state = new SchedulerState();

    /// <param name="now">Injectable clock (local time) for tests; defaults to <see cref="DateTime.Now"/>.</param>
    public SchedulerService(FileInfo stateFile, Func<DateTime> now = null) {
      this._stateFile = stateFile ?? throw new ArgumentNullException(nameof(stateFile));
      this._now = now ?? (() => DateTime.Now);
      this._Load();
    }

    private void _Load() {
      try {
        this._stateFile.Refresh();
        if (this._stateFile.Exists)
          this._state = JsonSerializer.Deserialize<SchedulerState>(File.ReadAllText(this._stateFile.FullName), _OPTIONS) ?? new SchedulerState();
      } catch (Exception) {

        // a broken state file only means schedules fire one extra time - never block startup over it
        this._state = new SchedulerState();
      }
    }

    private void _Save() {
      try {
        File.WriteAllText(this._stateFile.FullName, JsonSerializer.Serialize(this._state, _OPTIONS));
      } catch (IOException) {
        ;
      } catch (UnauthorizedAccessException) {
        ;
      }
    }

    private static string _Key(string rootPath, ScheduledAction action) => $"{rootPath}|{action}";

    /// <summary>Every action currently due, excluding ones already running.</summary>
    public IReadOnlyList<DueAction> GetDueActions(ConfigurationResolver resolver) {
      if (resolver == null) throw new ArgumentNullException(nameof(resolver));

      var now = this._now();
      var result = new List<DueAction>();

      lock (this._gate) {
        foreach (var root in resolver.WatchRoots) {
          var effective = resolver.Resolve(root.Path);

          this._ConsiderAction(result, root.Path, ScheduledAction.Verify, effective.VerifySchedule, now);

          if (!effective.BackupPath.IsNullOrWhiteSpace() && effective.BackupSchedule != null)
            this._ConsiderAction(result, root.Path, ScheduledAction.Backup, effective.BackupSchedule.Value, now);

          if (effective.RefreshIntervalDays > 0)
            this._ConsiderAction(result, root.Path, ScheduledAction.Refresh, ScheduleSpec.Every(TimeSpan.FromDays(effective.RefreshIntervalDays)), now);
        }
      }

      return result;
    }

    private void _ConsiderAction(List<DueAction> result, string rootPath, ScheduledAction action, ScheduleSpec schedule, DateTime now) {
      var key = _Key(rootPath, action);
      if (this._running.Contains(key))
        return;

      DateTime? lastRun = this._state.LastRuns.TryGetValue(key, out var timestamp)
        ? timestamp.ToLocalTime()
        : (DateTime?)null;

      if (schedule.NextDue(lastRun, now) <= now)
        result.Add(new DueAction(rootPath, action));
    }

    /// <summary>Claims the action; <c>false</c> when it is already running.</summary>
    public bool TryBeginRun(DueAction action) {
      lock (this._gate)
        return this._running.Add(_Key(action.RootPath, action.Action));
    }

    /// <summary>Records a successful completion and releases the claim.</summary>
    public void CompleteRun(DueAction action) {
      lock (this._gate) {
        this._running.Remove(_Key(action.RootPath, action.Action));
        this._state.LastRuns[_Key(action.RootPath, action.Action)] = this._now().ToUniversalTime();
        this._Save();
      }
    }

    /// <summary>Releases the claim without recording - the action stays due and is retried.</summary>
    public void AbortRun(DueAction action) {
      lock (this._gate)
        this._running.Remove(_Key(action.RootPath, action.Action));
    }

  }
}
