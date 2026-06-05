using System;
using System.Linq;
using System.Windows.Forms;
using Filesystem_Toolbox.Core.Configuration;

namespace Filesystem_Toolbox {

  /// <summary>
  /// Lets the user manage watched folders and their per-folder policies plus the global
  /// check interval. Works on a deep copy - nothing is applied until OK is pressed.
  /// </summary>
  internal sealed partial class SettingsForm : Form {

    private readonly ToolboxConfiguration _configuration;
    private bool _loadingSelection;

    /// <summary>The edited configuration; only meaningful when the dialog result is OK.</summary>
    public ToolboxConfiguration Result => this._configuration;

    public SettingsForm(ToolboxConfiguration current) {
      if (current == null) throw new ArgumentNullException(nameof(current));

      this._configuration = _Clone(current);
      this.InitializeComponent();
      this.SetFormTitle();
      this.Text += @" - Settings";

      this.nudCheckInterval.Value = Math.Max(this.nudCheckInterval.Minimum, Math.Min(this.nudCheckInterval.Maximum, this._configuration.CheckIntervalMinutes));
      this._ReloadFolderList(selectIndex: this._configuration.Folders.Count > 0 ? 0 : -1);
    }

    private static ToolboxConfiguration _Clone(ToolboxConfiguration source) => new ToolboxConfiguration {
      SchemaVersion = source.SchemaVersion,
      CheckIntervalMinutes = source.CheckIntervalMinutes,
      Folders = source.Folders.Select(f => new WatchedFolderConfiguration {
        Path = f.Path,
        ParityRedundancyPercent = f.ParityRedundancyPercent,
        AutoRepair = f.AutoRepair,
        MirrorPath = f.MirrorPath,
        RefreshIntervalDays = f.RefreshIntervalDays,
        OnCorruptionCommand = f.OnCorruptionCommand,
        DedupEnabled = f.DedupEnabled,
      }).ToList(),
    };

    private WatchedFolderConfiguration _SelectedFolder
      => this.lbFolders.SelectedIndex < 0 || this.lbFolders.SelectedIndex >= this._configuration.Folders.Count
        ? null
        : this._configuration.Folders[this.lbFolders.SelectedIndex]
      ;

    private void _ReloadFolderList(int selectIndex) {
      this.lbFolders.BeginUpdate();
      try {
        this.lbFolders.Items.Clear();
        foreach (var folder in this._configuration.Folders)
          this.lbFolders.Items.Add(folder.Path ?? "<unset>");
      } finally {
        this.lbFolders.EndUpdate();
      }

      this.lbFolders.SelectedIndex = Math.Min(selectIndex, this.lbFolders.Items.Count - 1);
      this._LoadSelectedFolder();
    }

    private void _LoadSelectedFolder() {
      var folder = this._SelectedFolder;
      this._loadingSelection = true;
      try {
        this.gbFolder.Enabled = folder != null;
        this.btnRemoveFolder.Enabled = folder != null;
        this.nudParityPercent.Value = folder == null ? 25 : Math.Max(0, Math.Min(100, folder.ParityRedundancyPercent));
        this.cbAutoRepair.Checked = folder?.AutoRepair ?? false;
        this.tbMirrorPath.Text = folder?.MirrorPath ?? string.Empty;
        this.nudRefreshDays.Value = folder == null ? 180 : Math.Max(0, Math.Min(3650, folder.RefreshIntervalDays));
        this.tbCommand.Text = folder?.OnCorruptionCommand ?? string.Empty;
        this.cbDedup.Checked = folder?.DedupEnabled ?? false;
      } finally {
        this._loadingSelection = false;
      }
    }

    private void lbFolders_SelectedIndexChanged(object sender, EventArgs e) => this._LoadSelectedFolder();

    private void btnAddFolder_Click(object sender, EventArgs e) {
      using (var dialog = new FolderBrowserDialog { Description = @"Select a folder to watch for silent corruption" }) {
        if (dialog.ShowDialog(this) != DialogResult.OK)
          return;

        this._configuration.Folders.Add(new WatchedFolderConfiguration { Path = dialog.SelectedPath });
        this._ReloadFolderList(this._configuration.Folders.Count - 1);
      }
    }

    private void btnRemoveFolder_Click(object sender, EventArgs e) {
      var index = this.lbFolders.SelectedIndex;
      if (index < 0)
        return;

      this._configuration.Folders.RemoveAt(index);
      this._ReloadFolderList(index);
    }

    private void btnBrowseMirror_Click(object sender, EventArgs e) {
      using (var dialog = new FolderBrowserDialog { Description = @"Select the mirror folder holding backup copies" }) {
        if (dialog.ShowDialog(this) == DialogResult.OK)
          this.tbMirrorPath.Text = dialog.SelectedPath;
      }
    }

    private void OnFolderSettingChanged(object sender, EventArgs e) {
      if (this._loadingSelection)
        return;

      var folder = this._SelectedFolder;
      if (folder == null)
        return;

      folder.ParityRedundancyPercent = (int)this.nudParityPercent.Value;
      folder.AutoRepair = this.cbAutoRepair.Checked;
      folder.MirrorPath = this.tbMirrorPath.Text.Trim().Length == 0 ? null : this.tbMirrorPath.Text.Trim();
      folder.RefreshIntervalDays = (int)this.nudRefreshDays.Value;
      folder.OnCorruptionCommand = this.tbCommand.Text.Trim().Length == 0 ? null : this.tbCommand.Text.Trim();
      folder.DedupEnabled = this.cbDedup.Checked;
    }

    private void btnOk_Click(object sender, EventArgs e) {
      this._configuration.CheckIntervalMinutes = (int)this.nudCheckInterval.Value;
      this.DialogResult = DialogResult.OK;
      this.Close();
    }

  }
}
