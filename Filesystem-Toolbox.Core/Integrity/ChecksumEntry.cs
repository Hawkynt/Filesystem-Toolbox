using System;
using System.Globalization;
using System.IO;

namespace Filesystem_Toolbox.Core.Integrity {

  /// <summary>
  /// One checksum.db value. Format v2 is <c>size:mtimeTicks:base64(sha512)</c>; the legacy
  /// v1 format <c>size:base64(sha512)</c> is still parsed (mtime unknown). The modification
  /// time is metadata, not identity: two entries are content-equal when size and hash match,
  /// no matter the timestamps - but a hash mismatch with UNCHANGED size+mtime is the
  /// signature of silent corruption (bit rot) rather than an intentional edit.
  /// </summary>
  public readonly struct ChecksumEntry {

    public long Size { get; }
    public long? ModificationTimeTicks { get; }
    public string HashBase64 { get; }

    public ChecksumEntry(long size, long? modificationTimeTicks, string hashBase64) {
      this.Size = size;
      this.ModificationTimeTicks = modificationTimeTicks;
      this.HashBase64 = hashBase64 ?? throw new ArgumentNullException(nameof(hashBase64));
    }

    public byte[] Hash => Convert.FromBase64String(this.HashBase64);

    /// <summary>Computes the entry for a file's current on-disk state.</summary>
    public static ChecksumEntry FromFile(FileInfo file) {
      if (file == null) throw new ArgumentNullException(nameof(file));

      file.Refresh();
      return new ChecksumEntry(file.Length, file.LastWriteTimeUtc.Ticks, Convert.ToBase64String(file.ComputeSHA512Hash()));
    }

    public static bool TryParse(string text, out ChecksumEntry entry) {
      entry = default;
      if (string.IsNullOrWhiteSpace(text))
        return false;

      var parts = text.Split(':');
      switch (parts.Length) {
        case 2: {
          if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
            return false;

          entry = new ChecksumEntry(size, null, parts[1]);
          return true;
        }
        case 3: {
          if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
            return false;

          if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
            return false;

          entry = new ChecksumEntry(size, ticks, parts[2]);
          return true;
        }
        default:
          return false;
      }
    }

    /// <summary>Same content - size and hash match; timestamps are irrelevant.</summary>
    public bool ContentEquals(ChecksumEntry other) => this.Size == other.Size && string.Equals(this.HashBase64, other.HashBase64, StringComparison.Ordinal);

    /// <summary>
    /// Was the file legitimately written to? Judged by the modification time alone - filesystem
    /// corruption can change the size (lost cluster chains) without any write ever happening,
    /// while real edits virtually always bump the timestamp. Unknown legacy timestamps count
    /// as untouched so that legacy mismatches conservatively surface as bit rot.
    /// </summary>
    public bool MetadataEquals(ChecksumEntry other)
      => this.ModificationTimeTicks == null
      || other.ModificationTimeTicks == null
      || this.ModificationTimeTicks == other.ModificationTimeTicks
      ;

    public override string ToString() => this.ModificationTimeTicks == null
      ? $"{this.Size}:{this.HashBase64}"
      : $"{this.Size}:{this.ModificationTimeTicks.Value}:{this.HashBase64}"
      ;

  }
}
