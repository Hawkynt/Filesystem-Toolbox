// ported from Hawkynt/DupMerge (LGPL-3.0)
using System;
using System.Collections.Generic;
using System.Threading;

namespace Filesystem_Toolbox.Core.Dedup {

  /// <summary>
  /// A pool of equally sized byte buffers that can be rented and returned, keeping GC pressure
  /// low during block-wise file comparison.
  /// </summary>
  internal sealed class BufferPool {

    public interface IRentBuffer : IDisposable {
      byte[] Buffer { get; }
      int Length { get; }
    }

    private sealed class RentBuffer : IRentBuffer {
      private readonly BufferPool _owner;
      private byte[] _buffer;

      public byte[] Buffer => this._buffer ?? throw new ObjectDisposedException(nameof(this.Buffer));
      public int Length => this._owner.BufferSize;

      public RentBuffer(BufferPool owner) {
        this._owner = owner;
        this._buffer = owner._Acquire();
      }

      ~RentBuffer() => this.Dispose();

      public void Dispose() {
        var buffer = Interlocked.Exchange(ref this._buffer, null);
        if (buffer != null)
          this._owner._Release(buffer);

        GC.SuppressFinalize(this);
      }
    }

    private readonly int _maxBuffersWaitingInPool;
    private readonly Stack<byte[]> _pool = new Stack<byte[]>();

    public int BufferSize { get; }

    public BufferPool(int bufferSize, int maxBuffersWaitingInPool = 64) {
      if (bufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(bufferSize));
      if (maxBuffersWaitingInPool <= 0) throw new ArgumentOutOfRangeException(nameof(maxBuffersWaitingInPool));

      this.BufferSize = bufferSize;
      this._maxBuffersWaitingInPool = maxBuffersWaitingInPool;
    }

    private void _Release(byte[] buffer) {
      lock (this._pool) {
        this._pool.Push(buffer);
        while (this._pool.Count > this._maxBuffersWaitingInPool)
          this._pool.Pop();
      }
    }

    private byte[] _Acquire() {
      var lockTaken = false;
      try {
        Monitor.TryEnter(this._pool, ref lockTaken);
        if (lockTaken && this._pool.Count > 0)
          return this._pool.Pop();
      } finally {
        if (lockTaken)
          Monitor.Exit(this._pool);
      }

      return new byte[this.BufferSize];
    }

    public IRentBuffer Use() => new RentBuffer(this);

  }
}
