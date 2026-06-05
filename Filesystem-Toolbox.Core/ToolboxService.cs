using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Filesystem_Toolbox.Core.Configuration;
using Filesystem_Toolbox.Core.Integrity;

namespace Filesystem_Toolbox.Core {
  public class ToolboxService : IDisposable {

    private static readonly DirectoryInfo _APPLICATION_FOLDER = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
    private const string _CONFIGURATION_FILE = "FilesystemToolbox.json";
    private const string _LEGACY_CONFIGURATION_FILE = "CheckedFolders.lst";

    private readonly List<FolderIntegrityChecker> _integrityCheckers = new List<FolderIntegrityChecker>();
    private readonly Dictionary<FolderIntegrityChecker, WatchedFolderConfiguration> _folderConfigurations = new Dictionary<FolderIntegrityChecker, WatchedFolderConfiguration>();

    private static FileInfo _ConfigurationFile => _APPLICATION_FOLDER.File(_CONFIGURATION_FILE);
    private static FileInfo _LegacyConfigurationFile => _APPLICATION_FOLDER.File(_LEGACY_CONFIGURATION_FILE);

    public ToolboxConfiguration Configuration { get; private set; } = new ToolboxConfiguration();

    public void SaveConfiguration() => ConfigurationStore.Save(this.Configuration, _ConfigurationFile);

    public void LoadConfiguration() {
      this._ClearCheckers();
      this.Configuration = ConfigurationStore.Load(_ConfigurationFile, _LegacyConfigurationFile);
      this._CreateCheckers();
    }

    /// <summary>
    /// Applies a new configuration: persists it and re-creates the folder checkers.
    /// </summary>
    public void ApplyConfiguration(ToolboxConfiguration configuration) {
      if (configuration == null) throw new ArgumentNullException(nameof(configuration));

      this._ClearCheckers();
      this.Configuration = configuration;
      this.SaveConfiguration();
      this._CreateCheckers();
    }

    public WatchedFolderConfiguration GetFolderConfiguration(FolderIntegrityChecker checker) {
      lock (this._integrityCheckers)
        return this._folderConfigurations.TryGetValue(checker, out var result) ? result : null;
    }

    public void RebuildDatabases() => this._ExecuteOnAllCheckers(c => c.RebuildDatabase());

    private void _ExecuteOnAllCheckers(Action<FolderIntegrityChecker> task) {
      if (task == null) throw new ArgumentNullException(nameof(task));

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

    public void AcceptChange(FolderIntegrityChecker checker, FileInfo file) => checker.UpdateFile(file);

    public void RunChecks(Action<FolderIntegrityChecker, FileInfo, string, string> onChecksumFailed, Action<FolderIntegrityChecker, FileInfo, string, Exception> onException)
      => this._ExecuteOnAllCheckers(c => c.VerifyIntegrity((f, o, n) => onChecksumFailed(c, f, o, n), (f, o, e) => onException(c, f, o, e)))
      ;

    private void _CreateCheckers() {
      foreach (var folder in this.Configuration.Folders) {
        if (folder.Path.IsNullOrWhiteSpace())
          continue;

        var rootDirectory = new DirectoryInfo(folder.Path);
        if (rootDirectory.NotExists())
          continue;

        var checker = FolderIntegrityChecker.Create(rootDirectory);
        lock (this._integrityCheckers) {
          this._integrityCheckers.Add(checker);
          this._folderConfigurations.Add(checker, folder);
        }

        checker.Enabled = true;
      }
    }

    private void _ClearCheckers() {
      FolderIntegrityChecker[] integrityCheckers;

      lock (this._integrityCheckers) {
        integrityCheckers = this._integrityCheckers.ToArray();
        this._integrityCheckers.Clear();
        this._folderConfigurations.Clear();
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

      this._ClearCheckers();
    }

    public void Dispose() {
      this._ReleaseUnmanagedResources();
      GC.SuppressFinalize(this);
    }

    ~ToolboxService() {
      this._ReleaseUnmanagedResources();
    }

    #endregion

  }
}
