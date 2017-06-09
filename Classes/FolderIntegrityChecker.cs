using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace Classes {

  class FolderIntegrityChecker : IDisposable {

    private const string _DATABASE_FILENAME = @".\checksum.db";

    private readonly FileSystemWatcher _fileSystemWatcher;
    private readonly ConcurrentDictionary<string, string> _database = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly FileInfo _databaseFile;
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
    }

    #region IDisposable

    private int _isDisposed;
    public bool IsDisposed => this._isDisposed != 0;

    private void _ReleaseUnmanagedResources() {
      if (Interlocked.CompareExchange(ref this._isDisposed, 1, 0) != 0)
        return;

      this.Enabled = false;
      this._fileSystemWatcher.Dispose();
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
    }

    private void _RemoveKey(string key) {
      string _;
      this._database.TryRemove(key, out _);
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
    }

    private void _TriggerDatabaseSave() {
      //TODO: only save every n seconds
      this._SaveDatabase();
    }

    private void _SaveDatabase() {
      // TODO: saveto file
    }

    private void _LoadDatabase() {
      // TODO: loadfrom file
    }

    private void _VerifyIntegrity() {
      // TODO: verify all files in db against their checksum
    }

    public static FolderIntegrityChecker Create(DirectoryInfo rootDirectory) => new FolderIntegrityChecker(rootDirectory);
    private static string _CalculateChecksum(FileInfo file) => file.Length + ":" + Convert.ToBase64String(file.ComputeSHA512Hash());
  }
}
