using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Filesystem_Toolbox.Properties;

namespace Filesystem_Toolbox {
  internal partial class MainForm : Form {

    #region nested types

    private class DgvEntry {
      private readonly FileInfo _file;
      private readonly Exception _exception;

      public Image Image { get; }
      public string FileName => this._file.Name;
      public string Extension => this._file.Extension;
      public string Path => this._file.Directory?.FullName;
      public string Checksum { get; }
      public string OldChecksum { get; }
      public string Exception => this._exception?.Message;

      private DgvEntry(FileInfo file) {
        this._file = file;
      }

      private DgvEntry(FileInfo file, string oldChecksum, string currentChecksum) : this(file) {
        this.Checksum = currentChecksum;
        this.OldChecksum = oldChecksum;
        this.Image = Resources._16x16_Warning;
      }

      private DgvEntry(FileInfo file, string oldChecksum, Exception exception) : this(file) {
        this._exception = exception;
        this.OldChecksum = oldChecksum;
        this.Image = Resources._16x16_Error;
      }

      public static DgvEntry FromFailedChecksum(FileInfo file, string old, string current) => new DgvEntry(file, old, current);
      public static DgvEntry FromException(FileInfo file, string old, Exception exception) => new DgvEntry(file, old, exception);
    }

    #endregion

    private readonly SortableBindingList<DgvEntry> _entries = new SortableBindingList<DgvEntry>();

    internal bool MarkVerificationRunning {
      set {
        this.SafelyInvoke(() => this.tsslVerificationRunning.Visible = value);
      }
    }

    internal MainForm() {
      this.InitializeComponent();
      this.dgvProblems.DataSource = this._entries;
    }

    internal void MarkFileChecksumFailed(FileInfo file, string oldChecksum, string newChecksum)
      => this.SafelyInvoke(() => this._entries.Add(DgvEntry.FromFailedChecksum(file, oldChecksum, newChecksum)))
      ;

    internal void MarkFileException(FileInfo file, string oldChecksum, Exception exception)
      => this.SafelyInvoke(() => this._entries.Add(DgvEntry.FromException(file, oldChecksum, exception)))
      ;
  }
}
