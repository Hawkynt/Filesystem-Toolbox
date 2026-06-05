using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Filesystem_Toolbox {

  internal enum UnrepairableChoice {
    Ignore,
    RestoreFromBackup,
    Rename,
    Delete,
  }

  /// <summary>
  /// Asks the user what to do with a file that could not be repaired: restore it from a
  /// backup snapshot, rename it to *.corrupt (keeping the evidence out of the way), delete
  /// it, or leave it as it is. Closing the dialog counts as Ignore.
  /// </summary>
  internal sealed class UnrepairableFileForm : Form {

    private readonly RadioButton _rbRestore;
    private readonly RadioButton _rbRename;
    private readonly RadioButton _rbDelete;
    private readonly RadioButton _rbIgnore;
    private readonly CheckBox _cbApplyToAll;

    public UnrepairableChoice Choice { get; private set; } = UnrepairableChoice.Ignore;

    /// <summary>Apply the same choice to all remaining unrepairable files of this batch.</summary>
    public bool ApplyToAll => this._cbApplyToAll.Checked;

    public UnrepairableFileForm(FileInfo file, bool backupAvailable) {
      if (file == null) throw new ArgumentNullException(nameof(file));

      this.Text = @"Unrepairable file";
      this.FormBorderStyle = FormBorderStyle.FixedDialog;
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.ShowInTaskbar = false;
      this.StartPosition = FormStartPosition.CenterParent;
      this.ClientSize = new Size(420, 250);

      var lblMessage = new Label {
        Text = $"{file.Name} could not be repaired.\r\nParity and backups cannot restore the recorded content.",
        Location = new Point(12, 12),
        Size = new Size(396, 34),
      };

      this._rbRestore = new RadioButton {
        Text = @"Restore from backup (search older snapshots again)",
        Location = new Point(24, 56),
        Size = new Size(384, 20),
        Enabled = backupAvailable,
      };
      this._rbRename = new RadioButton {
        Text = @"Rename to *.corrupt and stop tracking it",
        Location = new Point(24, 80),
        Size = new Size(384, 20),
      };
      this._rbDelete = new RadioButton {
        Text = @"Delete the file",
        Location = new Point(24, 104),
        Size = new Size(384, 20),
      };
      this._rbIgnore = new RadioButton {
        Text = @"Ignore (keep the damaged file as it is)",
        Location = new Point(24, 128),
        Size = new Size(384, 20),
        Checked = true,
      };

      this._cbApplyToAll = new CheckBox {
        Text = @"Apply this choice to all remaining unrepairable files",
        Location = new Point(12, 168),
        Size = new Size(396, 20),
      };

      var btnOk = new Button {
        Text = @"OK",
        DialogResult = DialogResult.OK,
        Location = new Point(252, 210),
        Size = new Size(75, 25),
      };
      btnOk.Click += (_, __) => this.Choice =
        this._rbRestore.Checked ? UnrepairableChoice.RestoreFromBackup
        : this._rbRename.Checked ? UnrepairableChoice.Rename
        : this._rbDelete.Checked ? UnrepairableChoice.Delete
        : UnrepairableChoice.Ignore;

      var btnCancel = new Button {
        Text = @"Cancel",
        DialogResult = DialogResult.Cancel,
        Location = new Point(333, 210),
        Size = new Size(75, 25),
      };

      this.AcceptButton = btnOk;
      this.CancelButton = btnCancel;
      this.Controls.AddRange(new Control[] { lblMessage, this._rbRestore, this._rbRename, this._rbDelete, this._rbIgnore, this._cbApplyToAll, btnOk, btnCancel });
    }

  }
}
