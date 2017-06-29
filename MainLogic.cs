using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Classes;

namespace Filesystem_Toolbox {
  class MainLogic : IDisposable {

    private static readonly DirectoryInfo _APPLICATION_FOLDER = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));
    private const string _INTEGRITY_CONFIGURATION_FILE = @".\CheckedFolders.lst";

    private readonly List<FolderIntegrityChecker> _integrityCheckers = new List<FolderIntegrityChecker>();
    private static FileInfo _IntegrityConfigurationFile => _APPLICATION_FOLDER.File(_INTEGRITY_CONFIGURATION_FILE);

    public void SaveConfiguration() {
      string[] integrityCheckedFolders;
      lock (this._integrityCheckers)
        integrityCheckedFolders = this._integrityCheckers.Select(i => i.RootDirectory.FullName).ToArray();

      _IntegrityConfigurationFile.WriteAllLines(integrityCheckedFolders);
    }

    public void LoadConfiguration() {
      this._ClearConfiguration();
      this.MergeConfiguration();
    }

    public void RebuildDatabases() => this._ExecuteOnAllCheckers(c => c.RebuildDatabase());

    private void _ExecuteOnAllCheckers(Action<FolderIntegrityChecker> task) {
      var alreadyRun = new HashSet<FolderIntegrityChecker>();
      while (true) {
        FolderIntegrityChecker currentChecker;
        lock (this._integrityCheckers)
          currentChecker = this._integrityCheckers.FirstOrDefault(i => !alreadyRun.Contains(i));

        if (currentChecker == null)
          return;

        alreadyRun.Add(currentChecker);
        task(currentChecker);
      }
    }

    public void RunChecks(Action<FileInfo, string, string> onChecksumFailed, Action<FileInfo, string, Exception> onException)
      => this._ExecuteOnAllCheckers(c => c.VerifyIntegrity(onChecksumFailed, onException))
      ;

    public void MergeConfiguration() {
      var lines = _IntegrityConfigurationFile.ReadAllLinesOrDefault();
      if (lines.IsNullOrEmpty())
        return;

      foreach (var line in lines) {
        if (line.IsNullOrWhiteSpace())
          continue;

        var rootDirectory = new DirectoryInfo(line);
        if (rootDirectory.NotExists())
          continue;

        var checker = FolderIntegrityChecker.Create(rootDirectory);
        lock (this._integrityCheckers)
          this._integrityCheckers.Add(checker);

        checker.Enabled = true;
      }
    }

    private void _ClearConfiguration() {
      FolderIntegrityChecker[] integrityCheckers;

      lock (this._integrityCheckers) {
        integrityCheckers = this._integrityCheckers.ToArray();
        this._integrityCheckers.Clear();
      }

      foreach (var checker in integrityCheckers)
        checker.Dispose();
    }

    #region IDisposable

    private int _isDisposed;
    public bool IsDisposed => this._isDisposed != 0;

    private void _ReleaseUnmanagedResources() {
      if (Interlocked.CompareExchange(ref this._isDisposed, 1, 0) != 0)
        return;

      this._ClearConfiguration();
    }

    public void Dispose() {
      this._ReleaseUnmanagedResources();
      GC.SuppressFinalize(this);
    }

    ~MainLogic() {
      this._ReleaseUnmanagedResources();
    }

    #endregion

  }
}
