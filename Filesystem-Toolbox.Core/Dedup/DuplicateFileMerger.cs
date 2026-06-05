// ported from Hawkynt/DupMerge (LGPL-3.0)
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Filesystem_Toolbox.Core.Dedup {

  /// <summary>
  /// Finds files with identical content (size bucket, then first/last-block SHA-512, then full
  /// block-wise comparison) and replaces the copies with hard links - NTFS only. Because NTFS
  /// hard links are not copy-on-write, new links can get the read-only attribute so an
  /// accidental edit through one name cannot silently change all the others.
  /// </summary>
  public static class DuplicateFileMerger {

    /// <summary>
    /// Processes the given folders with the given options, reporting progress via <paramref name="log"/>.
    /// </summary>
    public static DedupReport ProcessFolders(IList<DirectoryInfo> directories, DedupOptions options, Action<string> log = null) {
      if (directories == null) throw new ArgumentNullException(nameof(directories));
      if (options == null) throw new ArgumentNullException(nameof(options));

      var report = new DedupReport();
      var seenFiles = new ConcurrentDictionary<long, ConcurrentDictionary<string, FileEntry>>();
      var stack = new ConcurrentStack<DirectoryInfo>();
      stack.PushRange(directories.ToArray());
      report.IncrementFolders(directories.Count);
      var threads = new Thread[Math.Max(1, options.MaximumCrawlerThreads)];

      using (var autoResetEvent = new AutoResetEvent(false)) {
        var runningWorkers = new[] { threads.Length };

        for (var i = 0; i < threads.Length; ++i) {
          threads[i] = new Thread(() => _ThreadWorker(stack, options, report, seenFiles, autoResetEvent, runningWorkers, log));
          threads[i].Start();
        }

        foreach (var thread in threads)
          thread.Join();
      }

      return report;
    }

    private static void _ThreadWorker(
      ConcurrentStack<DirectoryInfo> stack,
      DedupOptions options,
      DedupReport report,
      ConcurrentDictionary<long, ConcurrentDictionary<string, FileEntry>> seenItems,
      EventWaitHandle waiter,
      int[] state,
      Action<string> log
    ) {
      while (true) {
        if (!stack.TryPop(out var current)) {

          // when stack is empty, signal we're lazy and if all other threads are also, end thread
          if (Interlocked.Decrement(ref state[0]) == 0) {

            // signal another thread to continue exiting
            waiter.Set();
            return;
          }

          waiter.WaitOne();
          Interlocked.Increment(ref state[0]);
          continue;
        }

        // push directories and wake up any sleeping threads
        foreach (var directory in current.SafelyEnumerateDirectories()) {
          if (options.DirectoryFilter != null && !options.DirectoryFilter(directory))
            continue;

          stack.Push(directory);
          report.IncrementFolders();

          // notify other threads which may be waiting for work
          waiter.Set();
        }

        foreach (var item in current.SafelyEnumerateFiles())
          _HandleFile(item, options, report, seenItems, log);
      }
    }

    private static void _HandleFile(
      FileInfo item,
      DedupOptions options,
      DedupReport report,
      ConcurrentDictionary<long, ConcurrentDictionary<string, FileEntry>> seenItems,
      Action<string> log
    ) {
      report.IncrementFiles();
      var length = item.Length;
      report.IncrementBytes(length);

      if (length < options.MinimumFileSizeInBytes || length > options.MaximumFileSizeInBytes)
        return;

      var knownWithThisLength = seenItems.GetOrAdd(length, _ => new ConcurrentDictionary<string, FileEntry>());

      // preventing other threads from processing files with the same size
      // avoiding a race condition where all known links to a file are removed at once, thus loosing data completely
      lock (knownWithThisLength)
        try {
          _HandleFileWithGivenSize(item, options, report, knownWithThisLength, log);
        } catch (Exception e) {
          report.IncrementErrors();
          log?.Invoke($"Could not process file {item.FullName}: {e.Message}");
        }
    }

    private static void _HandleFileWithGivenSize(
      FileInfo item,
      DedupOptions options,
      DedupReport report,
      ConcurrentDictionary<string, FileEntry> knownWithThisLength,
      Action<string> log
    ) {
      var myKey = _GenerateKey(item);
      var checksum = knownWithThisLength.GetOrAdd(myKey, new FileEntry(item));

      var isHardLink = false;
      IEnumerable<FileInfo> hardlinks;

      try {
        hardlinks = item.GetHardLinkTargets().Where(i => i.FullName != item.FullName);
      } catch (Exception e) {
        _RemoveFileEntry(item, knownWithThisLength);
        report.IncrementErrors();
        log?.Invoke($"Could not enumerate hard links of {item.FullName}: {e.Message}");
        return;
      }

      foreach (var target in hardlinks) {
        isHardLink = true;
        knownWithThisLength.TryAdd(_GenerateKey(target), new FileEntry(target));
      }

      if (isHardLink) {

        // already linked to something - nothing to gain here
        report.IncrementLinksSeen();
        return;
      }

      string symlink;

      try {
        symlink = item.GetSymbolicLinkTarget();
      } catch (Exception e) {
        _RemoveFileEntry(item, knownWithThisLength);
        report.IncrementErrors();
        log?.Invoke($"Could not enumerate symbolic link of {item.FullName}: {e.Message}");
        return;
      }

      if (symlink != null) {
        knownWithThisLength.TryAdd(symlink, new FileEntry(new FileInfo(symlink)));
        report.IncrementLinksSeen();
        return;
      }

      // find matching file in seen list and try to hard or symlink
      var sameFiles =
          knownWithThisLength
            .Where(kvp => kvp.Key != myKey)
            .Where(kvp => kvp.Value.Equals(checksum))
            .Select(kvp => kvp.Key)
        ;

      foreach (var sameFile in sameFiles) {
        if (options.ShowInfoOnly) {
          log?.Invoke($"Duplicate found (dry run): {item.FullName} == {sameFile}");
          report.IncrementHardLinksCreated();
          return;
        }

        var temporaryFile = _CreateTemporaryFileInSameDirectory(item);
        temporaryFile.Delete();

        var isSymlink = false;

        try {
          temporaryFile.CreateHardLinkFrom(sameFile);
        } catch (Exception e1) {
          if (options.AlsoTrySymbolicLinks) {
            isSymlink = true;
            try {
              temporaryFile.CreateSymbolicLinkFrom(sameFile);
            } catch (Exception e2) {
              report.IncrementErrors();
              log?.Invoke($"Could not symlink {item.FullName} --> {sameFile}: {e2.Message}");
              continue;
            }
          } else {
            report.IncrementErrors();
            log?.Invoke($"Could not hardlink {item.FullName} --> {sameFile}: {e1.Message}");
            continue;
          }
        }

        var isAlreadyDeleted = false;

        try {
          item.Attributes &= ~FileAttributes.ReadOnly;
          item.Delete();
          isAlreadyDeleted = true;
          File.Move(temporaryFile.FullName, item.FullName);
        } catch {
          if (isAlreadyDeleted) {

            // undo file deletion
            temporaryFile.CopyTo(item.FullName, true);
          } else {

            // undo temp file creation
            temporaryFile.Delete();
          }

          throw;
        }

        if (isSymlink) {
          report.IncrementSymbolicLinksCreated();
          if (options.SetReadOnlyAttributeOnNewSymbolicLinks)
            item.Attributes |= FileAttributes.ReadOnly;
        } else {
          report.IncrementHardLinksCreated();
          if (options.SetReadOnlyAttributeOnNewHardLinks)
            item.Attributes |= FileAttributes.ReadOnly;
        }

        log?.Invoke($"Created {(isSymlink ? "symlink" : "hardlink")} {item.FullName} --> {sameFile}");
        return;
      }
    }

    private static void _RemoveFileEntry(FileInfo item, ConcurrentDictionary<string, FileEntry> knownWithThisLength)
      => knownWithThisLength.TryRemove(_GenerateKey(item), out _)
      ;

    private static FileInfo _CreateTemporaryFileInSameDirectory(FileInfo file) {
      const int ERROR_FILE_EXISTS = unchecked((int)0x80070050);
      var name = file.FullName;

      while (true) {
        var result = new FileInfo(name + ".$$$");
        name = result.FullName;
        if (result.Exists)
          continue;

        try {
          var fileHandle = result.Open(FileMode.CreateNew, FileAccess.Write);
          fileHandle.Close();
          result.Refresh();
          return result;
        } catch (IOException e) when (e.HResult == ERROR_FILE_EXISTS) {
          // possibly another process created the file already
        }
      }
    }

    private static string _GenerateKey(FileInfo file) => file.FullName;

  }
}
