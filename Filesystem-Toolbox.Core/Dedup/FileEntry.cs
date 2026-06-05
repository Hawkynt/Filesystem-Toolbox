// ported from Hawkynt/DupMerge (LGPL-3.0)
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Filesystem_Toolbox.Core.Dedup {

  /// <summary>
  /// Represents a file already seen by the crawlers, encapsulating details such as its checksum,
  /// and providing utilities for comparison.
  /// </summary>
  internal sealed class FileEntry {

    private static class NativeMethods {

      [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "GetDiskFreeSpaceW")]
      [return: MarshalAs(UnmanagedType.Bool)]
      private static extern bool _GetDiskFreeSpace(
        string lpRootPathName,
        out uint lpSectorsPerCluster,
        out uint lpBytesPerSector,
        out uint lpNumberOfFreeClusters,
        out uint lpTotalNumberOfClusters
      );

      /// <summary>Bytes per cluster of the volume holding the given entry.</summary>
      public static long GetBytesPerCluster(FileSystemInfo entry) {
        if (!_GetDiskFreeSpace(Path.GetPathRoot(entry.FullName) ?? ".", out var sectorsPerCluster, out var bytesPerSector, out _, out _))
          throw new Win32Exception(Marshal.GetLastWin32Error());

        return (long)sectorsPerCluster * bytesPerSector;
      }

    }

    private const int _FALLBACK_BLOCK_SIZE = 4 * 1024 * 1024;

    private static BufferPool _pool;
    private static BufferPool _Pool => _pool;

    private static readonly byte[] _EMPTY_BYTES = new byte[0];

    private readonly Lazy<byte[]> _checksum;

    private static void _EnsurePoolInitialized(FileSystemInfo anyEntryFromFilesystem) {
      if (_pool != null)
        return;

      lock (typeof(FileEntry)) {
        if (_pool != null)
          return;

        int blockSize;
        try {
          blockSize = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? (int)(256 * NativeMethods.GetBytesPerCluster(anyEntryFromFilesystem))
            : _FALLBACK_BLOCK_SIZE
            ;
        } catch (Exception) {
          blockSize = _FALLBACK_BLOCK_SIZE;
        }

        _pool = new BufferPool(blockSize);
      }
    }

    public FileEntry(FileInfo source) {
      _EnsurePoolInitialized(source);

      this._Source = source;
      this._checksum = new Lazy<byte[]>(this._CalculateChecksum);
      this._FileSize = source.Length;
    }

    private FileInfo _Source { get; }
    private long _FileSize { get; }
    private byte[] _Checksum => this._checksum.Value;

    /// <summary>
    /// Calculates a quick checksum.
    /// NOTE: In our case we create SHA512 by using the first and last block (if available)
    /// </summary>
    private byte[] _CalculateChecksum() {
      var length = this._FileSize;
      if (length <= 0)
        return _EMPTY_BYTES;

      using var stream = new FileStream(this._Source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);

      const int checksumLength = 512 / 8;

      // for small files, don't hash - use their contents
      if (length < checksumLength) {
        var result = new byte[length];
        var _ = stream.Read(result, 0, result.Length);
        return result;
      }

      using var provider = SHA512.Create();

      using var rented = _Pool.Use();
      var buffer = rented.Buffer;

      // read first block
      var bufferLength = rented.Length;
      var bytesRead = stream.Read(buffer, 0, bufferLength);
      if (length > bufferLength) {
        provider.TransformBlock(buffer, 0, bytesRead, buffer, 0);

        // read last block (or what is left of it)
        stream.Seek(Math.Max(bufferLength, length - bufferLength), SeekOrigin.Begin);
        bytesRead = stream.Read(buffer, 0, bufferLength);
      }

      provider.TransformFinalBlock(buffer, 0, bytesRead);

      return provider.Hash ?? _EMPTY_BYTES;
    }

    /// <summary>
    /// Tests if the given entry has equal file content to this entry.
    /// </summary>
    public bool Equals(FileEntry other) {
      try {
        var myLength = this._FileSize;

        // NOTE: STEP 1: compare sizes - should always equal because we make sure that only same size files are compared by the business logic
        if (myLength != other._FileSize)
          return false;

        if (myLength == 0)
          return true;

        // NOTE: STEP 2: compare checksums, hopefully this saves us from comparing byte-by-byte and because checksums are cached in-memory we also spare some re-read I/O
        var sourceChecksum = this._Checksum;
        var comparisonChecksum = other._Checksum;
        if (!BlockComparer.IsEqual(sourceChecksum, sourceChecksum.Length, comparisonChecksum, comparisonChecksum.Length))
          return false;

        // NOTE: STEP 3: compare bytewise
        using var sourceStream = new FileStream(this._Source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var comparisonStream = new FileStream(other._Source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);

        var bufferLength = _Pool.BufferSize;

        // NOTE: we're going to compare buffers (A, A') while reading the next blocks (B, B') in already
        using var sba = _Pool.Use();
        using var cba = _Pool.Use();
        var sourceBufferA = sba.Buffer;
        var comparisonBufferA = cba.Buffer;

        using var sbb = _Pool.Use();
        using var cbb = _Pool.Use();
        var sourceBufferB = sbb.Buffer;
        var comparisonBufferB = cbb.Buffer;

        var blockCount = Math.DivRem(myLength, bufferLength, out var lastBlockSize);

        // if there are bytes left in a partly filled last block - we need one block more
        if (lastBlockSize != 0)
          ++blockCount;

        using var enumerator = BlockIndexShuffler.Shuffle(blockCount).GetEnumerator();

        // NOTE: should never land here, because only 0-byte files would get us an empty enumerator
        if (!enumerator.MoveNext())
          return false;

        var blockIndex = enumerator.Current;

        // start reading buffers into A and A'
        var sourceAsync = sourceStream.ReadBytesAsync(blockIndex * bufferLength, sourceBufferA);
        var comparisonAsync = comparisonStream.ReadBytesAsync(blockIndex * bufferLength, comparisonBufferA);
        int sourceBytes;
        int comparisonBytes;

        while (enumerator.MoveNext()) {
          sourceBytes = sourceAsync.Result;
          comparisonBytes = comparisonAsync.Result;

          // start reading next buffers into B and B'
          blockIndex = enumerator.Current;
          sourceAsync = sourceStream.ReadBytesAsync(blockIndex * bufferLength, sourceBufferB);
          comparisonAsync = comparisonStream.ReadBytesAsync(blockIndex * bufferLength, comparisonBufferB);

          // compare A and A' and return false upon difference
          if (!BlockComparer.IsEqual(sourceBufferA, sourceBytes, comparisonBufferA, comparisonBytes))
            return false;

          // switch A and B and A' and B'
          (sourceBufferA, sourceBufferB, comparisonBufferA, comparisonBufferB)
            = (sourceBufferB, sourceBufferA, comparisonBufferB, comparisonBufferA)
            ;
        }

        // compare A and A'
        sourceBytes = sourceAsync.Result;
        comparisonBytes = comparisonAsync.Result;
        return BlockComparer.IsEqual(sourceBufferA, sourceBytes, comparisonBufferA, comparisonBytes);
      } catch (Exception) {

        // if either file could not be read - assume they are not equal because we can't be sure
        return false;
      }
    }

  }
}
