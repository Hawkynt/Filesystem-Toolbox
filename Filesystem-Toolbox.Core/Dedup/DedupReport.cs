using System.Threading;

namespace Filesystem_Toolbox.Core.Dedup {

  /// <summary>Thread-safe counters describing one dedup run.</summary>
  public sealed class DedupReport {

    private long _foldersScanned;
    private long _filesScanned;
    private long _bytesScanned;
    private long _hardLinksCreated;
    private long _symbolicLinksCreated;
    private long _linksSeen;
    private long _errors;

    public long FoldersScanned => Interlocked.Read(ref this._foldersScanned);
    public long FilesScanned => Interlocked.Read(ref this._filesScanned);
    public long BytesScanned => Interlocked.Read(ref this._bytesScanned);
    public long HardLinksCreated => Interlocked.Read(ref this._hardLinksCreated);
    public long SymbolicLinksCreated => Interlocked.Read(ref this._symbolicLinksCreated);
    public long LinksSeen => Interlocked.Read(ref this._linksSeen);
    public long Errors => Interlocked.Read(ref this._errors);

    internal void IncrementFolders(long count = 1) => Interlocked.Add(ref this._foldersScanned, count);
    internal void IncrementFiles() => Interlocked.Increment(ref this._filesScanned);
    internal void IncrementBytes(long count) => Interlocked.Add(ref this._bytesScanned, count);
    internal void IncrementHardLinksCreated() => Interlocked.Increment(ref this._hardLinksCreated);
    internal void IncrementSymbolicLinksCreated() => Interlocked.Increment(ref this._symbolicLinksCreated);
    internal void IncrementLinksSeen() => Interlocked.Increment(ref this._linksSeen);
    internal void IncrementErrors() => Interlocked.Increment(ref this._errors);

    /// <summary>Accumulates another run's counters into this report.</summary>
    public void Merge(DedupReport other) {
      Interlocked.Add(ref this._foldersScanned, other.FoldersScanned);
      Interlocked.Add(ref this._filesScanned, other.FilesScanned);
      Interlocked.Add(ref this._bytesScanned, other.BytesScanned);
      Interlocked.Add(ref this._hardLinksCreated, other.HardLinksCreated);
      Interlocked.Add(ref this._symbolicLinksCreated, other.SymbolicLinksCreated);
      Interlocked.Add(ref this._linksSeen, other.LinksSeen);
      Interlocked.Add(ref this._errors, other.Errors);
    }

  }
}
