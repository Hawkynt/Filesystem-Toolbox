using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Filesystem_Toolbox.Core.Scheduling;

namespace Filesystem_Toolbox.Core.Integrity {

  public class FolderIntegrityChecker : IDisposable {

    private const string _DATABASE_FILENAME = @".\checksum.db";

    private readonly FileSystemWatcher _fileSystemWatcher;
    private readonly ConcurrentDictionary<string, string> _database = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly FileInfo _databaseFile;
    private readonly ScheduledTask _scheduledTask;
    private readonly TaskQueue _taskQueue = new TaskQueue { RequeueOnException = true };
    public DirectoryInfo RootDirectory { get; }

    public bool Enabled {
      get { return this._fileSystemWatcher.EnableRaisingEvents; }
      set { this._fileSystemWatcher.EnableRaisingEvents = value; }
    }

    public IEnumerable<FileInfo> KnownFiles => this._database.Keys.Select(k => this.RootDirectory.File(k));

    public FolderIntegrityChecker(DirectoryInfo rootDirectory) {
      this.RootDirectory = rootDirectory;
      this._databaseFile = rootDirectory.File(_DATABASE_FILENAME);

      var watcher = this._fileSystemWatcher = new FileSystemWatcher(rootDirectory.FullName);
      watcher.NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
      watcher.Changed += this._FileSystemWatcher_OnChanged;
      watcher.Created += this._FileSystemWatcher_OnChanged;
      watcher.Deleted += this._FileSystemWatcher_OnDeleted;
      watcher.Renamed += this._FileSystemWatcher_OnRenamed;
      watcher.InternalBufferSize = 65536;

      this._scheduledTask = new ScheduledTask(this._Scheduler_OnExecute, deferredTime: TimeSpan.FromSeconds(10));

    }

    #region IDisposable

    private int _isDisposed;
    public bool IsDisposed => this._isDisposed != 0;

    private void _ReleaseUnmanagedResources() {
      if (Interlocked.CompareExchange(ref this._isDisposed, 1, 0) != 0)
        return;

      this.Enabled = false;
      this._fileSystemWatcher.Dispose();
      this._taskQueue.Dispose();
      this._TrySaveDatabase();
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

    private void _Scheduler_OnExecute() => this._TrySaveDatabase();

    /// <summary>
    /// Saves the database, swallowing I/O errors - the root (e.g. an unplugged USB stick)
    /// may have vanished since the save was scheduled, and a deferred save must never
    /// take down the process from a timer thread.
    /// </summary>
    private void _TrySaveDatabase() {
      if (this.IsDisposed && this._database.IsEmpty)
        return;

      try {
        this.SaveDatabase();
      } catch (IOException) {
        ;
      } catch (UnauthorizedAccessException) {
        ;
      }
    }

    private void _FileSystemWatcher_OnRenamed(object _, RenamedEventArgs e) {
      var newFile = new FileInfo(e.FullPath);
      var oldFile = new FileInfo(e.OldFullPath);

      if (this._IsIgnoredFile(newFile) || this._IsIgnoredFile(oldFile))
        return;

      this._EnqueueTask(
        () => this._ChangeFileName(oldFile, newFile),
        oldFile,
        newFile
      );
    }

    private void _FileSystemWatcher_OnDeleted(object _, FileSystemEventArgs e) {
      var file = new FileInfo(e.FullPath);
      if (this._IsIgnoredFile(file))
        return;

      this._EnqueueTask(() => this._RemoveFile(file), file);
    }

    private void _FileSystemWatcher_OnChanged(object _, FileSystemEventArgs e) {
      var file = new FileInfo(e.FullPath);
      if (this._IsIgnoredFile(file))
        return;

      this._EnqueueTask(() => this._AddOrUpdateFile(file), file);
    }

    #endregion

    /// <summary>Name of the hidden per-root folder holding parity and refresh state - never watched or verified.</summary>
    public const string PROTECTED_FOLDER_NAME = ".fst";

    /// <summary>Raised after a file's database entry was added or updated (carries the new entry).</summary>
    public event Action<FileInfo, ChecksumEntry> EntryUpdated;

    /// <summary>Raised after a file's database entry was removed.</summary>
    public event Action<FileInfo> EntryRemoved;

    /// <summary>Raised after a rename moved an entry from one file to another.</summary>
    public event Action<FileInfo, FileInfo> EntryRenamed;

    private bool _IsIgnoredFile(FileInfo file)
      => file.FullName == this._databaseFile.FullName
      || this._IsInProtectedFolder(file.FullName)
      ;

    private bool _IsInProtectedFolder(string fullName)
      => fullName.StartsWith(Path.Combine(this.RootDirectory.FullName, PROTECTED_FOLDER_NAME) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private bool _IsIgnoredDirectory(DirectoryInfo directory)
      => string.Equals(directory.Name, PROTECTED_FOLDER_NAME, StringComparison.OrdinalIgnoreCase)
      && string.Equals(directory.Parent?.FullName, this.RootDirectory.FullName, StringComparison.OrdinalIgnoreCase)
      ;

    private void _EnqueueTask(Action task, FileInfo file, FileInfo alternateFile = null) {
      var key = this._GetKey(file);
      this._taskQueue.DequeueByTag(key);
      if (alternateFile != null)
        this._taskQueue.DequeueByTag(this._GetKey(alternateFile));

      this._taskQueue.Enqueue(task, key);
    }

    private void _AddOrUpdateFile(FileInfo file) {
      var entry = ChecksumEntry.FromFile(file);
      var value = entry.ToString();
      this._database.AddOrUpdate(this._GetKey(file), _ => value, (_, __) => value);

      this._TriggerDatabaseSave();
      this.EntryUpdated?.Invoke(file, entry);
    }

    private void _RemoveFile(FileInfo file) {
      string _;
      if (!this._database.TryRemove(this._GetKey(file), out _))
        return;

      this._TriggerDatabaseSave();
      this.EntryRemoved?.Invoke(file);
    }

    private void _ChangeFileName(FileInfo oldFile, FileInfo newFile) {
      if (oldFile == null) throw new ArgumentNullException(nameof(oldFile));
      if (newFile == null) throw new ArgumentNullException(nameof(newFile));

      string oldChecksum;
      this._database.TryAdd(this._GetKey(newFile),
        this._database.TryRemove(this._GetKey(oldFile), out oldChecksum)
          ? oldChecksum
          : _CalculateChecksum(newFile)
      );

      this._TriggerDatabaseSave();
      this.EntryRenamed?.Invoke(oldFile, newFile);
    }

    private void _TriggerDatabaseSave() => this._scheduledTask.Schedule();

    public void UpdateFile(FileInfo file) {
      if (file.FullName.StartsNotWith(this.RootDirectory.FullName))
        throw new ArgumentException("File is not relative to root directory", nameof(file));

      file.Refresh();
      if (file.NotExists()) {
        this._RemoveFile(file);
        return;
      }

      try {
        this._AddOrUpdateFile(file);
      } catch (Exception) {
        ;
      }
    }

    public void RebuildDatabase() {
      var stack = new Stack<DirectoryInfo>();
      stack.Push(this.RootDirectory);
      this._database.Clear();
      while (stack.Count > 0) {
        var current = stack.Pop();
        foreach (var fsi in current.EnumerateFileSystemInfos()) {
          var dir = fsi as DirectoryInfo;
          if (dir != null) {
            if (!this._IsIgnoredDirectory(dir))
              stack.Push(dir);

            continue;
          }

          var file = fsi as FileInfo;
          if (file == null || this._IsIgnoredFile(file))
            continue;

          try {
            this._AddOrUpdateFile(file);
          } catch (Exception) {
            ;
          }
        }
      }
    }

    public void SaveDatabase() {
      var file = this._databaseFile;
      lock (file) {
        file.Refresh();
        if (file.Exists)
          file.Attributes &= ~(FileAttributes.System | FileAttributes.Hidden);

        file.WriteAllLines(this._database.OrderBy(i => new string('\\', i.Key.Count(c => c == '\\')) + i.Key).Select(kvp => $@"{kvp.Value} => {kvp.Key}"));
        file.TryEnableCompression();
        file.Attributes |= FileAttributes.System | FileAttributes.Hidden;

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

          var index = line.IndexOf("=>", StringComparison.Ordinal);
          if (index < 0)
            continue;

          var value = line.Left(index).TrimEnd();
          var key = line.Substring(index + 2).TrimStart();
          this._database.TryAdd(key, value);
        }
      }
    }

    public void VerifyIntegrity(Action<FileInfo, string, string> onInvalidChecksum, Action<FileInfo, string, Exception> onException = null) {
      if (onInvalidChecksum == null) throw new ArgumentNullException(nameof(onInvalidChecksum));

      foreach (var entry in this._database) {
        var file = this.RootDirectory.File(entry.Key);
        var expected = entry.Value;
        string current;
        try {
          current = _CalculateChecksum(file);
        } catch (Exception exception) {
          onException?.Invoke(file, expected, exception);
          continue;
        }
        // semantic comparison: timestamps are metadata, only size + hash define content
        if (ChecksumEntry.TryParse(expected, out var expectedEntry)
            && ChecksumEntry.TryParse(current, out var currentEntry)
            && currentEntry.ContentEquals(expectedEntry))
          continue;

        onInvalidChecksum(file, expected, current);
      }

      foreach (
        var file in
          this.RootDirectory.EnumerateFiles("*.*", SearchOption.AllDirectories)
            .Where(f => !(this._IsIgnoredFile(f) || this._database.ContainsKey(this._GetKey(f))))) {
        string current;
        try {
          current = _CalculateChecksum(file);
        } catch (Exception exception) {
          onException?.Invoke(file, null, exception);
          continue;
        }
        onInvalidChecksum(file, null, current);
      }
    }

    public static FolderIntegrityChecker Create(DirectoryInfo rootDirectory) {
      var result = new FolderIntegrityChecker(rootDirectory);
      result.LoadDatabase();
      return result;
    }

    private static string _CalculateChecksum(FileInfo file) => ChecksumEntry.FromFile(file).ToString();
    private string _GetKey(FileInfo file) => file.RelativeTo(this.RootDirectory);

    /// <summary>A point-in-time copy of the database: relative path to serialized <see cref="ChecksumEntry"/>.</summary>
    public Dictionary<string, string> GetDatabaseSnapshot() => new Dictionary<string, string>(this._database);

    /// <summary>Looks up the stored entry for a file, if any.</summary>
    public bool TryGetEntry(FileInfo file, out ChecksumEntry entry) {
      entry = default;
      return this._database.TryGetValue(this._GetKey(file), out var value) && ChecksumEntry.TryParse(value, out entry);
    }

    /// <summary>Resolves a database key back to its absolute file.</summary>
    public FileInfo GetFile(string databaseKey) => this.RootDirectory.File(databaseKey);

  }
}
