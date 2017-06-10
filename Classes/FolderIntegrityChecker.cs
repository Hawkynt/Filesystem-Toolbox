using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// TODO: internal queue for processing files
// TODO: requeue file on file access exception

namespace Classes {

  class FolderIntegrityChecker : IDisposable {

    private const string _DATABASE_FILENAME = @".\checksum.db";

    private readonly FileSystemWatcher _fileSystemWatcher;
    private readonly ConcurrentDictionary<string, string> _database = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly FileInfo _databaseFile;
    private readonly ScheduledTask _scheduledTask;
    public DirectoryInfo RootDirectory { get; }

    public bool Enabled {
      get { return this._fileSystemWatcher.EnableRaisingEvents; }
      set { this._fileSystemWatcher.EnableRaisingEvents = value; }
    }

    public FolderIntegrityChecker(DirectoryInfo rootDirectory) {
      this.RootDirectory = rootDirectory;
      this._databaseFile = rootDirectory.File(_DATABASE_FILENAME);

      var watcher = this._fileSystemWatcher = new FileSystemWatcher(rootDirectory.FullName);
      watcher.NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
      watcher.Changed += this._FileSystemWatcher_OnChanged;
      watcher.Created += this._FileSystemWatcher_OnCreated;
      watcher.Deleted += this._FileSystemWatcher_OnDeleted;
      watcher.Renamed += this._FileSystemWatcher_OnRenamed;

      this._scheduledTask = new ScheduledTask(this._Scheduler_OnExecute, deferredTime: TimeSpan.FromMinutes(5));

    }

    #region IDisposable

    private int _isDisposed;
    public bool IsDisposed => this._isDisposed != 0;

    private void _ReleaseUnmanagedResources() {
      if (Interlocked.CompareExchange(ref this._isDisposed, 1, 0) != 0)
        return;

      this.Enabled = false;
      this._fileSystemWatcher.Dispose();
      this.SaveDatabase();
    }

    public void Dispose() {
      this._ReleaseUnmanagedResources();
      GC.SuppressFinalize(this);
    }

    ~FolderIntegrityChecker() {
      this._ReleaseUnmanagedResources();
    }

    #endregion

    #region event handlers

    private void _Scheduler_OnExecute() => this.SaveDatabase();

    private void _FileSystemWatcher_OnRenamed(object _, RenamedEventArgs e) {
      if (this._IsIgnoredFile(e.FullPath) || this._IsIgnoredFile(e.OldFullPath))
        return;

      this._ChangeKeyName(e.OldFullPath, e.FullPath, () => _CalculateChecksum(new FileInfo(e.FullPath)));
    }

    private void _FileSystemWatcher_OnDeleted(object _, FileSystemEventArgs e) {
      if (this._IsIgnoredFile(e.FullPath))
        return;

      this._RemoveKey(e.FullPath);
    }

    private void _FileSystemWatcher_OnCreated(object _, FileSystemEventArgs e) {
      if (this._IsIgnoredFile(e.FullPath))
        return;

      this._AddOrUpdateKey(e.FullPath, _CalculateChecksum(new FileInfo(e.FullPath)));
    }

    private void _FileSystemWatcher_OnChanged(object _, FileSystemEventArgs e) {
      if (this._IsIgnoredFile(e.FullPath))
        return;

      this._AddOrUpdateKey(e.FullPath, _CalculateChecksum(new FileInfo(e.FullPath)));
    }

    #endregion

    private bool _IsIgnoredFile(string fileName) => fileName == this._databaseFile.FullName;

    private void _AddOrUpdateKey(string key, string value) {
      this._database.AddOrUpdate(key, _ => value, (_, __) => value);

      this._TriggerDatabaseSave();
    }

    private void _RemoveKey(string key) {
      string _;
      this._database.TryRemove(key, out _);

      this._TriggerDatabaseSave();
    }

    private void _ChangeKeyName(string oldKey, string newKey, Func<string> valueFactory) {
      if (valueFactory == null) throw new ArgumentNullException(nameof(valueFactory));

      string oldChecksum;
      this._database.TryAdd(
        newKey,
        this._database.TryRemove(oldKey, out oldChecksum)
          ? oldChecksum
          : valueFactory()
      );

      this._TriggerDatabaseSave();
    }

    private void _TriggerDatabaseSave() => this._scheduledTask.Schedule();

    public void SaveDatabase() {
      var file = this._databaseFile;
      lock (file) {
        file.WriteAllLines(this._database.Select(kvp => $@"{kvp.Value} = {kvp.Key}"));
        file.Attributes |= FileAttributes.System;
        file.TryEnableCompression();
      }
    }

    public void LoadDatabase() {
      this._database.Clear();
      var file = this._databaseFile;
      lock (file) {
        file.Refresh();
        if (!file.Exists)
          return;

        foreach (var line in file.ReadLines()) {
          if (line.IsNullOrWhiteSpace())
            continue;

          var index = line.IndexOf("=", StringComparison.Ordinal);
          if (index < 0)
            continue;

          var value = line.Left(index).TrimEnd();
          var key = line.Substring(index).TrimStart();
          this._database.TryAdd(key, value);
        }
      }
    }

    public void VerifyIntegrity(Action<FileInfo, string, string> onInvalidChecksum, Action<FileInfo, string, Exception> onException = null) {
      if (onInvalidChecksum == null) throw new ArgumentNullException(nameof(onInvalidChecksum));

      foreach (var entry in this._database) {
        var file = new FileInfo(entry.Key);
        var expected = entry.Value;
        string current;
        try {
          current = _CalculateChecksum(file);
        } catch (Exception exception) {
          onException?.Invoke(file, expected, exception);
          continue;
        }
        if (current == expected)
          continue;

        onInvalidChecksum(file, expected, current);
      }
    }

    public static FolderIntegrityChecker Create(DirectoryInfo rootDirectory) {
      var result = new FolderIntegrityChecker(rootDirectory);
      result.LoadDatabase();
      return result;
    }

    private static string _CalculateChecksum(FileInfo file) => file.Length + ":" + Convert.ToBase64String(file.ComputeSHA512Hash());
  }
}
