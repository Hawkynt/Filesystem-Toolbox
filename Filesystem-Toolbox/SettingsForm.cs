using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Filesystem_Toolbox.Core.Configuration;
using Filesystem_Toolbox.Core.Scheduling;

namespace Filesystem_Toolbox {

  /// <summary>
  /// Manages watched folders and overrides as a tree: top-level nodes are watch roots,
  /// nested nodes override settings for their subtree. Every setting row has an
  /// "override" checkbox - unchecked means the value is inherited (shown grayed) from
  /// the nearest configured ancestor, the global configuration or the defaults; removing
  /// an override restores the chain. Works on a deep copy; nothing applies until OK.
  /// </summary>
  internal sealed partial class SettingsForm : Form {

    #region field binding plumbing

    private sealed class FieldBinding {
      public CheckBox Override;
      public Control[] ValueControls;
      public Func<WatchedFolderConfiguration, bool> HasValue;
      public Action<WatchedFolderConfiguration> Clear;
      public Action<WatchedFolderConfiguration> SaveFromControls;
      public Action<EffectiveSettings> ShowEffective;
      public Action<WatchedFolderConfiguration> ShowOwn;
    }

    private readonly List<FieldBinding> _bindings = new List<FieldBinding>();
    private bool _loadingSelection;

    #endregion

    private readonly ToolboxConfiguration _configuration;

    /// <summary>The edited configuration; only meaningful when the dialog result is OK.</summary>
    public ToolboxConfiguration Result => this._configuration;

    public SettingsForm(ToolboxConfiguration current) {
      if (current == null) throw new ArgumentNullException(nameof(current));

      this._configuration = _Clone(current);
      this.InitializeComponent();
      this.SetFormTitle();
      this.Text += @" - Settings";

      this._CreateBindings();
      this.tbGlobalSchedule.Text = (this._configuration.VerifySchedule ?? ConfigurationDefaults.VERIFY_SCHEDULE).ToString();
      this._ReloadTree(selectPath: this._configuration.Folders.FirstOrDefault()?.Path);
    }

    private static ToolboxConfiguration _Clone(ToolboxConfiguration source) => new ToolboxConfiguration {
      SchemaVersion = source.SchemaVersion,
      VerifySchedule = source.VerifySchedule,
      Folders = source.Folders.Select(f => new WatchedFolderConfiguration {
        Path = f.Path,
        ParityRedundancyPercent = f.ParityRedundancyPercent,
        AutoRepair = f.AutoRepair,
        RefreshIntervalDays = f.RefreshIntervalDays,
        OnCorruptionCommand = f.OnCorruptionCommand,
        DedupEnabled = f.DedupEnabled,
        VerifySchedule = f.VerifySchedule,
        BackupPath = f.BackupPath,
        BackupSchedule = f.BackupSchedule,
        GfsKeepDaily = f.GfsKeepDaily,
        GfsKeepWeekly = f.GfsKeepWeekly,
        GfsKeepMonthly = f.GfsKeepMonthly,
        DegradationWarningErrorsPerMonth = f.DegradationWarningErrorsPerMonth,
        ToastNotifications = f.ToastNotifications,
      }).ToList(),
    };

    private WatchedFolderConfiguration _SelectedFolder => this.tvFolders.SelectedNode?.Tag as WatchedFolderConfiguration;

    /// <summary>What the selected path would inherit if this entry did not override anything.</summary>
    private EffectiveSettings _InheritedSettings(WatchedFolderConfiguration entry) {
      ScheduleSpec? globalSchedule = ScheduleSpec.TryParse(this.tbGlobalSchedule.Text, out var parsed) ? parsed : this._configuration.VerifySchedule;
      return new ConfigurationResolver(this._configuration.Folders.Where(f => !ReferenceEquals(f, entry)), globalSchedule).Resolve(entry.Path);
    }

    #region bindings

    private void _CreateBindings() {
      this._Bind(this.cbOvParity, new Control[] { this.nudParityPercent },
        f => f.ParityRedundancyPercent != null,
        f => f.ParityRedundancyPercent = null,
        f => f.ParityRedundancyPercent = (int)this.nudParityPercent.Value,
        e => this.nudParityPercent.Value = Math.Min(100, e.ParityRedundancyPercent),
        f => this.nudParityPercent.Value = Math.Min(100, f.ParityRedundancyPercent ?? 0));

      this._Bind(this.cbOvAutoRepair, new Control[] { this.cbAutoRepair },
        f => f.AutoRepair != null,
        f => f.AutoRepair = null,
        f => f.AutoRepair = this.cbAutoRepair.Checked,
        e => this.cbAutoRepair.Checked = e.AutoRepair,
        f => this.cbAutoRepair.Checked = f.AutoRepair ?? false);

      this._Bind(this.cbOvVerifySchedule, new Control[] { this.tbVerifySchedule },
        f => f.VerifySchedule != null,
        f => f.VerifySchedule = null,
        f => f.VerifySchedule = this._ParseSchedule(this.tbVerifySchedule, allowEmpty: false) ?? f.VerifySchedule,
        e => this.tbVerifySchedule.Text = e.VerifySchedule.ToString(),
        f => this.tbVerifySchedule.Text = f.VerifySchedule?.ToString() ?? string.Empty);

      this._Bind(this.cbOvBackupPath, new Control[] { this.tbBackupPath, this.btnBrowseBackup },
        f => !string.IsNullOrWhiteSpace(f.BackupPath),
        f => f.BackupPath = null,
        f => f.BackupPath = this.tbBackupPath.Text.Trim().Length == 0 ? null : this.tbBackupPath.Text.Trim(),
        e => this.tbBackupPath.Text = e.BackupPath ?? string.Empty,
        f => this.tbBackupPath.Text = f.BackupPath ?? string.Empty);

      this._Bind(this.cbOvBackupSchedule, new Control[] { this.tbBackupSchedule },
        f => f.BackupSchedule != null,
        f => f.BackupSchedule = null,
        f => f.BackupSchedule = this._ParseSchedule(this.tbBackupSchedule, allowEmpty: true),
        e => this.tbBackupSchedule.Text = e.BackupSchedule?.ToString() ?? string.Empty,
        f => this.tbBackupSchedule.Text = f.BackupSchedule?.ToString() ?? string.Empty);

      this._Bind(this.cbOvGfs, new Control[] { this.nudGfsDaily, this.nudGfsWeekly, this.nudGfsMonthly },
        f => f.GfsKeepDaily != null || f.GfsKeepWeekly != null || f.GfsKeepMonthly != null,
        f => { f.GfsKeepDaily = null; f.GfsKeepWeekly = null; f.GfsKeepMonthly = null; },
        f => { f.GfsKeepDaily = (int)this.nudGfsDaily.Value; f.GfsKeepWeekly = (int)this.nudGfsWeekly.Value; f.GfsKeepMonthly = (int)this.nudGfsMonthly.Value; },
        e => { this.nudGfsDaily.Value = e.GfsKeepDaily; this.nudGfsWeekly.Value = e.GfsKeepWeekly; this.nudGfsMonthly.Value = e.GfsKeepMonthly; },
        f => { this.nudGfsDaily.Value = f.GfsKeepDaily ?? 0; this.nudGfsWeekly.Value = f.GfsKeepWeekly ?? 0; this.nudGfsMonthly.Value = f.GfsKeepMonthly ?? 0; });

      this._Bind(this.cbOvRefresh, new Control[] { this.nudRefreshDays },
        f => f.RefreshIntervalDays != null,
        f => f.RefreshIntervalDays = null,
        f => f.RefreshIntervalDays = (int)this.nudRefreshDays.Value,
        e => this.nudRefreshDays.Value = Math.Min(3650, e.RefreshIntervalDays),
        f => this.nudRefreshDays.Value = Math.Min(3650, f.RefreshIntervalDays ?? 0));

      this._Bind(this.cbOvCommand, new Control[] { this.tbCommand },
        f => !string.IsNullOrWhiteSpace(f.OnCorruptionCommand),
        f => f.OnCorruptionCommand = null,
        f => f.OnCorruptionCommand = this.tbCommand.Text.Trim().Length == 0 ? null : this.tbCommand.Text.Trim(),
        e => this.tbCommand.Text = e.OnCorruptionCommand ?? string.Empty,
        f => this.tbCommand.Text = f.OnCorruptionCommand ?? string.Empty);

      this._Bind(this.cbOvDedup, new Control[] { this.cbDedup },
        f => f.DedupEnabled != null,
        f => f.DedupEnabled = null,
        f => f.DedupEnabled = this.cbDedup.Checked,
        e => this.cbDedup.Checked = e.DedupEnabled,
        f => this.cbDedup.Checked = f.DedupEnabled ?? false);

      this._Bind(this.cbOvDegradation, new Control[] { this.nudDegradation },
        f => f.DegradationWarningErrorsPerMonth != null,
        f => f.DegradationWarningErrorsPerMonth = null,
        f => f.DegradationWarningErrorsPerMonth = (int)this.nudDegradation.Value,
        e => this.nudDegradation.Value = Math.Min(1000, e.DegradationWarningErrorsPerMonth),
        f => this.nudDegradation.Value = Math.Min(1000, f.DegradationWarningErrorsPerMonth ?? 1));

      this._Bind(this.cbOvToasts, new Control[] { this.cbToasts },
        f => f.ToastNotifications != null,
        f => f.ToastNotifications = null,
        f => f.ToastNotifications = this.cbToasts.Checked,
        e => this.cbToasts.Checked = e.ToastNotifications,
        f => this.cbToasts.Checked = f.ToastNotifications ?? true);
    }

    private void _Bind(
      CheckBox overrideBox,
      Control[] valueControls,
      Func<WatchedFolderConfiguration, bool> hasValue,
      Action<WatchedFolderConfiguration> clear,
      Action<WatchedFolderConfiguration> saveFromControls,
      Action<EffectiveSettings> showEffective,
      Action<WatchedFolderConfiguration> showOwn
    ) {
      var binding = new FieldBinding {
        Override = overrideBox,
        ValueControls = valueControls,
        HasValue = hasValue,
        Clear = clear,
        SaveFromControls = saveFromControls,
        ShowEffective = showEffective,
        ShowOwn = showOwn,
      };
      this._bindings.Add(binding);

      overrideBox.CheckedChanged += (_, __) => this._OnOverrideToggled(binding);
      foreach (var control in valueControls)
        switch (control) {
          case NumericUpDown nud: nud.ValueChanged += (_, __) => this._OnValueChanged(binding); break;
          case TextBox tb: tb.TextChanged += (_, __) => this._OnValueChanged(binding); break;
          case CheckBox cb: cb.CheckedChanged += (_, __) => this._OnValueChanged(binding); break;
        }
    }

    private void _OnOverrideToggled(FieldBinding binding) {
      if (this._loadingSelection)
        return;

      var folder = this._SelectedFolder;
      if (folder == null)
        return;

      this._loadingSelection = true;
      try {
        if (binding.Override.Checked) {

          // seed the control with the currently inherited value, then make it the override
          binding.ShowEffective(this._InheritedSettings(folder));
          this._SetEnabled(binding, true);
          binding.SaveFromControls(folder);
        } else {
          binding.Clear(folder);
          this._SetEnabled(binding, false);
          binding.ShowEffective(this._InheritedSettings(folder));
        }
      } finally {
        this._loadingSelection = false;
      }
    }

    private void _OnValueChanged(FieldBinding binding) {
      if (this._loadingSelection || !binding.Override.Checked)
        return;

      var folder = this._SelectedFolder;
      if (folder != null)
        binding.SaveFromControls(folder);
    }

    private void _SetEnabled(FieldBinding binding, bool enabled) {
      foreach (var control in binding.ValueControls) {
        control.Enabled = enabled;
        control.ForeColor = enabled ? SystemColors.ControlText : SystemColors.GrayText;
      }
    }

    private ScheduleSpec? _ParseSchedule(TextBox box, bool allowEmpty) {
      var text = box.Text.Trim();
      if (text.Length == 0) {
        box.BackColor = allowEmpty ? SystemColors.Window : Color.MistyRose;
        return null;
      }

      if (ScheduleSpec.TryParse(text, out var result)) {
        box.BackColor = SystemColors.Window;
        return result;
      }

      box.BackColor = Color.MistyRose;
      return null;
    }

    #endregion

    #region tree handling

    private void _ReloadTree(string selectPath) {
      this.tvFolders.BeginUpdate();
      try {
        this.tvFolders.Nodes.Clear();

        var entries = this._configuration.Folders
          .Where(f => !string.IsNullOrWhiteSpace(f.Path))
          .OrderBy(f => f.Path.Length)
          .ToList();

        var nodesByEntry = new Dictionary<WatchedFolderConfiguration, TreeNode>();
        foreach (var entry in entries) {

          // deepest other entry that is a strict ancestor becomes the parent node
          var parent = entries
            .Where(other => !ReferenceEquals(other, entry)
                            && entry.Path.StartsWith(other.Path.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(other => other.Path.Length)
            .FirstOrDefault();

          var node = new TreeNode(parent == null ? entry.Path : entry.Path.Substring(parent.Path.TrimEnd('\\').Length + 1)) {
            Tag = entry,
            ToolTipText = entry.Path,
          };
          nodesByEntry[entry] = node;

          if (parent != null && nodesByEntry.TryGetValue(parent, out var parentNode))
            parentNode.Nodes.Add(node);
          else
            this.tvFolders.Nodes.Add(node);
        }

        this.tvFolders.ExpandAll();

        var toSelect = nodesByEntry.FirstOrDefault(p => string.Equals(p.Key.Path, selectPath, StringComparison.OrdinalIgnoreCase)).Value
                       ?? this.tvFolders.Nodes.Cast<TreeNode>().FirstOrDefault();
        this.tvFolders.SelectedNode = toSelect;
      } finally {
        this.tvFolders.EndUpdate();
      }

      this._LoadSelectedFolder();
    }

    private void _LoadSelectedFolder() {
      var folder = this._SelectedFolder;
      this._loadingSelection = true;
      try {
        this.gbFolder.Enabled = folder != null;
        this.btnRemove.Enabled = folder != null;
        this.btnAddOverride.Enabled = folder != null;

        if (folder == null)
          return;

        var inherited = this._InheritedSettings(folder);
        foreach (var binding in this._bindings) {
          var overridden = binding.HasValue(folder);
          binding.Override.Checked = overridden;
          this._SetEnabled(binding, overridden);
          if (overridden)
            binding.ShowOwn(folder);
          else
            binding.ShowEffective(inherited);
        }
      } finally {
        this._loadingSelection = false;
      }
    }

    private void tvFolders_AfterSelect(object sender, TreeViewEventArgs e) => this._LoadSelectedFolder();

    private void btnAddRoot_Click(object sender, EventArgs e) {
      using (var dialog = new FolderBrowserDialog { Description = @"Select a folder to watch for silent corruption" }) {
        if (dialog.ShowDialog(this) != DialogResult.OK)
          return;

        this._configuration.Folders.Add(new WatchedFolderConfiguration { Path = dialog.SelectedPath });
        this._ReloadTree(dialog.SelectedPath);
      }
    }

    private void btnAddOverride_Click(object sender, EventArgs e) {
      var parent = this._SelectedFolder;
      if (parent == null)
        return;

      using (var dialog = new FolderBrowserDialog {
        Description = $@"Select a subfolder of {parent.Path} whose settings should differ",
        SelectedPath = parent.Path,
      }) {
        if (dialog.ShowDialog(this) != DialogResult.OK)
          return;

        if (!dialog.SelectedPath.StartsWith(parent.Path.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) {
          MessageBox.Show(this, @"An override must live inside the selected folder.", @"Add override", MessageBoxButtons.OK, MessageBoxIcon.Warning);
          return;
        }

        this._configuration.Folders.Add(new WatchedFolderConfiguration { Path = dialog.SelectedPath });
        this._ReloadTree(dialog.SelectedPath);
      }
    }

    private void btnRemove_Click(object sender, EventArgs e) {
      var folder = this._SelectedFolder;
      if (folder == null)
        return;

      this._configuration.Folders.Remove(folder); // nested overrides of a removed root become roots themselves - intentional
      this._ReloadTree(null);
    }

    #endregion

    private void btnBrowseBackup_Click(object sender, EventArgs e) {
      using (var dialog = new FolderBrowserDialog { Description = @"Select the backup target (GFS snapshots are created beneath it)" }) {
        if (dialog.ShowDialog(this) == DialogResult.OK)
          this.tbBackupPath.Text = dialog.SelectedPath;
      }
    }

    private void btnOk_Click(object sender, EventArgs e) {
      var globalSchedule = this._ParseSchedule(this.tbGlobalSchedule, allowEmpty: true);
      if (this.tbGlobalSchedule.Text.Trim().Length > 0 && globalSchedule == null) {
        MessageBox.Show(this, @"The global verify schedule is invalid. Use e.g. 'every 10m', 'daily 03:30' or 'weekly Sunday 03:30'.", @"Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
      }

      this._configuration.VerifySchedule = globalSchedule;
      this.DialogResult = DialogResult.OK;
      this.Close();
    }

  }
}
