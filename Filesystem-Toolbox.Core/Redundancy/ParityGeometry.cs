using System;

namespace Filesystem_Toolbox.Core.Redundancy {

  /// <summary>
  /// The fixed stripe geometry of the parity store: a file is processed in stripes of
  /// <see cref="DataShards"/> x <see cref="ShardSize"/> data bytes; per stripe,
  /// <see cref="ParityShardCount"/> parity shards are kept. The tail stripe is virtually
  /// zero-padded - the padding is implied by the original length and never stored.
  /// </summary>
  internal readonly struct ParityGeometry {

    public const int DEFAULT_SHARD_SIZE = 64 * 1024;
    public const int DEFAULT_DATA_SHARDS = 16;
    private const int _MAX_TOTAL_SHARDS = 255;

    public int ShardSize { get; }
    public int DataShards { get; }
    public int ParityShardCount { get; }

    public long StripeDataBytes => (long)this.ShardSize * this.DataShards;

    public ParityGeometry(int shardSize, int dataShards, int parityShards) {
      if (shardSize < 1) throw new ArgumentOutOfRangeException(nameof(shardSize));
      if (dataShards < 1) throw new ArgumentOutOfRangeException(nameof(dataShards));
      if (parityShards < 1 || dataShards + parityShards > _MAX_TOTAL_SHARDS) throw new ArgumentOutOfRangeException(nameof(parityShards));

      this.ShardSize = shardSize;
      this.DataShards = dataShards;
      this.ParityShardCount = parityShards;
    }

    /// <summary>
    /// Derives the geometry from a redundancy percentage: m = ceil(k * percent / 100),
    /// clamped to [1, 255-k]. 25% with the default k=16 yields 4 parity shards, i.e.
    /// up to four damaged 64 KiB regions per MiB are repairable.
    /// </summary>
    public static ParityGeometry FromRedundancyPercent(int percent) {
      if (percent < 0) throw new ArgumentOutOfRangeException(nameof(percent));

      var parityShards = (DEFAULT_DATA_SHARDS * percent + 99) / 100;
      parityShards = Math.Max(1, Math.Min(_MAX_TOTAL_SHARDS - DEFAULT_DATA_SHARDS, parityShards));
      return new ParityGeometry(DEFAULT_SHARD_SIZE, DEFAULT_DATA_SHARDS, parityShards);
    }

    public long GetStripeCount(long fileLength) {
      if (fileLength < 0) throw new ArgumentOutOfRangeException(nameof(fileLength));

      return (fileLength + this.StripeDataBytes - 1) / this.StripeDataBytes;
    }

  }
}
