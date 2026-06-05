using System;
using System.IO;
using Filesystem_Toolbox.Core.Integrity;
using Filesystem_Toolbox.Core.Redundancy;
using Filesystem_Toolbox.Core.Scheduling;

namespace Filesystem_Toolbox.Core.Services {

  /// <summary>
  /// Keeps parity bound to the checksum database: whenever the checker records a new or
  /// changed entry the parity is rebuilt in the background (debounced per file - rapid
  /// successive edits collapse into one build), removals delete the parity, renames move it.
  /// </summary>
  public sealed class ParityMaintenanceQueue : IDisposable {

    private readonly FolderIntegrityChecker _checker;
    private readonly ParityStore _store;
    private readonly TaskQueue _queue = new TaskQueue();

    public ParityMaintenanceQueue(FolderIntegrityChecker checker, ParityStore store) {
      this._checker = checker ?? throw new ArgumentNullException(nameof(checker));
      this._store = store ?? throw new ArgumentNullException(nameof(store));

      checker.EntryUpdated += this._OnEntryUpdated;
      checker.EntryRemoved += this._OnEntryRemoved;
      checker.EntryRenamed += this._OnEntryRenamed;
    }

    private void _OnEntryUpdated(FileInfo file, ChecksumEntry entry) {
      var tag = file.FullName;
      this._queue.DequeueByTag(tag);
      this._queue.Enqueue(() => this._TryBuildParity(file), tag);
    }

    private void _OnEntryRemoved(FileInfo file) {
      var tag = file.FullName;
      this._queue.DequeueByTag(tag);
      this._queue.Enqueue(() => this._Try(() => this._store.DeleteParity(file)), tag);
    }

    private void _OnEntryRenamed(FileInfo oldFile, FileInfo newFile) {
      this._queue.DequeueByTag(oldFile.FullName);
      this._queue.DequeueByTag(newFile.FullName);
      this._queue.Enqueue(() => this._Try(() => this._store.MoveParity(oldFile, newFile)), newFile.FullName);
    }

    private void _TryBuildParity(FileInfo file) => this._Try(() => {
      file.Refresh();
      if (!file.Exists)
        return;

      this._store.BuildParity(file);
    });

    /// <summary>
    /// Background parity work must never crash anything: a file still being written or an
    /// unplugged medium simply means the next change event triggers another attempt.
    /// </summary>
    private void _Try(Action action) {
      try {
        action();
      } catch (IOException) {
        ;
      } catch (UnauthorizedAccessException) {
        ;
      }
    }

    public void Dispose() {
      this._checker.EntryUpdated -= this._OnEntryUpdated;
      this._checker.EntryRemoved -= this._OnEntryRemoved;
      this._checker.EntryRenamed -= this._OnEntryRenamed;
      this._queue.Dispose();
    }

  }
}
