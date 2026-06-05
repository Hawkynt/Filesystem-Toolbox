using System;
using System.IO;

namespace Filesystem_Toolbox.Core.Dedup {

  /// <summary>Settings for the duplicate-to-hardlink merger.</summary>
  public sealed class DedupOptions {

    /// <summary>Files below this size are skipped. The default is 1 byte (empty files are never linked).</summary>
    public long MinimumFileSizeInBytes { get; set; } = 1;

    /// <summary>Files above this size are skipped.</summary>
    public long MaximumFileSizeInBytes { get; set; } = long.MaxValue;

    /// <summary>Falls back to symbolic links when hard linking fails (e.g. across volumes).</summary>
    public bool AlsoTrySymbolicLinks { get; set; }

    /// <summary>
    /// Sets the read-only attribute on newly created hard links - works around NTFS hard links
    /// not being copy-on-write: an accidental edit through one name would silently change all
    /// other names too, so read-only forces the user to consciously remove the attribute first.
    /// </summary>
    public bool SetReadOnlyAttributeOnNewHardLinks { get; set; } = true;

    /// <summary>Sets the read-only attribute on newly created symbolic links.</summary>
    public bool SetReadOnlyAttributeOnNewSymbolicLinks { get; set; }

    /// <summary>Number of crawler threads. The default is the lesser of the processor count or 8.</summary>
    public int MaximumCrawlerThreads { get; set; } = Math.Min(Environment.ProcessorCount, 8);

    /// <summary>Dry run: report what would be linked without changing anything.</summary>
    public bool ShowInfoOnly { get; set; }

    /// <summary>Optional predicate deciding which directories are crawled; <c>null</c> crawls everything.</summary>
    public Func<DirectoryInfo, bool> DirectoryFilter { get; set; }

  }
}
