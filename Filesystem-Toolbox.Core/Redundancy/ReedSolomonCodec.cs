using System;

namespace Filesystem_Toolbox.Core.Redundancy {

  /// <summary>
  /// Systematic Reed-Solomon erasure code over GF(2^8): k data shards pass through unchanged,
  /// m parity shards are linear combinations. Any k surviving shards out of k+m reconstruct
  /// the original data, i.e. up to m known-position losses (erasures) are repairable.
  /// </summary>
  /// <remarks>
  /// The coding matrix is a Vandermonde matrix normalized to systematic form (multiplied by the
  /// inverse of its top square) - this guarantees that every possible k-row submatrix is invertible.
  /// </remarks>
  internal sealed class ReedSolomonCodec {

    private const int _MAX_TOTAL_SHARDS = 255;

    /// <summary>Rows k..k+m-1 of the systematic coding matrix (the parity coefficient rows).</summary>
    private readonly byte[][] _parityRows;

    public int DataShards { get; }
    public int ParityShards { get; }
    public int TotalShards { get; }

    /// <exception cref="ArgumentOutOfRangeException">when k &lt; 1, m &lt; 1 or k+m &gt; 255</exception>
    public ReedSolomonCodec(int dataShards, int parityShards) {
      if (dataShards < 1) throw new ArgumentOutOfRangeException(nameof(dataShards), dataShards, "need at least one data shard");
      if (parityShards < 1) throw new ArgumentOutOfRangeException(nameof(parityShards), parityShards, "need at least one parity shard");
      if (dataShards + parityShards > _MAX_TOTAL_SHARDS) throw new ArgumentOutOfRangeException(nameof(parityShards), dataShards + parityShards, $"GF(2^8) supports at most {_MAX_TOTAL_SHARDS} total shards");

      this.DataShards = dataShards;
      this.ParityShards = parityShards;
      this.TotalShards = dataShards + parityShards;
      this._parityRows = _BuildParityRows(dataShards, parityShards);
    }

    /// <summary>
    /// Builds the bottom m rows of the systematic coding matrix A = V * inverse(V_top),
    /// where V is the (k+m) x k Vandermonde matrix over GF(2^8).
    /// </summary>
    private static byte[][] _BuildParityRows(int k, int m) {
      var vandermonde = new byte[k + m][];
      for (var row = 0; row < k + m; ++row) {
        vandermonde[row] = new byte[k];
        for (var column = 0; column < k; ++column)
          vandermonde[row][column] = GaloisField256.Power((byte)row, column);
      }

      var top = new byte[k][];
      for (var row = 0; row < k; ++row)
        top[row] = (byte[])vandermonde[row].Clone();

      var topInverse = InvertMatrix(top);

      var result = new byte[m][];
      for (var row = 0; row < m; ++row) {
        result[row] = new byte[k];
        for (var column = 0; column < k; ++column) {
          byte sum = 0;
          for (var i = 0; i < k; ++i)
            sum ^= GaloisField256.Multiply(vandermonde[k + row][i], topInverse[i][column]);

          result[row][column] = sum;
        }
      }

      return result;
    }

    /// <summary>
    /// Inverts a square matrix over GF(2^8) using Gauss-Jordan elimination.
    /// </summary>
    /// <exception cref="InvalidOperationException">when the matrix is singular</exception>
    internal static byte[][] InvertMatrix(byte[][] matrix) {
      var n = matrix.Length;
      var work = new byte[n][];
      var result = new byte[n][];
      for (var row = 0; row < n; ++row) {
        work[row] = (byte[])matrix[row].Clone();
        result[row] = new byte[n];
        result[row][row] = 1;
      }

      for (var column = 0; column < n; ++column) {

        // find a pivot row
        var pivot = -1;
        for (var row = column; row < n; ++row)
          if (work[row][column] != 0) {
            pivot = row;
            break;
          }

        if (pivot < 0)
          throw new InvalidOperationException("Matrix is singular");

        if (pivot != column) {
          (work[pivot], work[column]) = (work[column], work[pivot]);
          (result[pivot], result[column]) = (result[column], result[pivot]);
        }

        // normalize the pivot row
        var scale = GaloisField256.Inverse(work[column][column]);
        for (var i = 0; i < n; ++i) {
          work[column][i] = GaloisField256.Multiply(work[column][i], scale);
          result[column][i] = GaloisField256.Multiply(result[column][i], scale);
        }

        // eliminate the column from all other rows
        for (var row = 0; row < n; ++row) {
          if (row == column)
            continue;

          var factor = work[row][column];
          if (factor == 0)
            continue;

          for (var i = 0; i < n; ++i) {
            work[row][i] ^= GaloisField256.Multiply(factor, work[column][i]);
            result[row][i] ^= GaloisField256.Multiply(factor, result[column][i]);
          }
        }
      }

      return result;
    }

    /// <summary>
    /// Computes the parity shards from the data shards. All shards must be of equal length.
    /// </summary>
    public void Encode(byte[][] dataShards, byte[][] parityShards, int shardLength) {
      if (dataShards == null) throw new ArgumentNullException(nameof(dataShards));
      if (parityShards == null) throw new ArgumentNullException(nameof(parityShards));
      if (dataShards.Length != this.DataShards) throw new ArgumentException($"expected {this.DataShards} data shards", nameof(dataShards));
      if (parityShards.Length != this.ParityShards) throw new ArgumentException($"expected {this.ParityShards} parity shards", nameof(parityShards));

      for (var row = 0; row < this.ParityShards; ++row) {
        Array.Clear(parityShards[row], 0, shardLength);
        var coefficients = this._parityRows[row];
        for (var column = 0; column < this.DataShards; ++column)
          GaloisField256.MultiplyAndAddRegion(coefficients[column], dataShards[column], 0, parityShards[row], 0, shardLength);
      }
    }

    /// <summary>
    /// Reconstructs missing shards in place. <paramref name="shardPresent"/> flags which of the
    /// k+m shards (data first, then parity) are trustworthy; the rest are treated as erased.
    /// </summary>
    /// <returns><c>false</c> when fewer than k shards survive - unrecoverable.</returns>
    public bool DecodeErasures(byte[][] shards, bool[] shardPresent, int shardLength) {
      if (shards == null) throw new ArgumentNullException(nameof(shards));
      if (shardPresent == null) throw new ArgumentNullException(nameof(shardPresent));
      if (shards.Length != this.TotalShards) throw new ArgumentException($"expected {this.TotalShards} shards", nameof(shards));
      if (shardPresent.Length != this.TotalShards) throw new ArgumentException($"expected {this.TotalShards} flags", nameof(shardPresent));

      var k = this.DataShards;

      var presentCount = 0;
      foreach (var flag in shardPresent)
        if (flag)
          ++presentCount;

      if (presentCount < k)
        return false;

      var allDataPresent = true;
      for (var i = 0; i < k; ++i)
        if (!shardPresent[i]) {
          allDataPresent = false;
          break;
        }

      if (!allDataPresent) {

        // pick the first k surviving shards and the matching coding-matrix rows
        var survivorRows = new byte[k][];
        var survivors = new byte[k][];
        var survivorIndex = 0;
        for (var shard = 0; shard < this.TotalShards && survivorIndex < k; ++shard) {
          if (!shardPresent[shard])
            continue;

          survivorRows[survivorIndex] = this._GetCodingRow(shard);
          survivors[survivorIndex] = shards[shard];
          ++survivorIndex;
        }

        var decodeMatrix = InvertMatrix(survivorRows);

        // recompute every missing data shard
        for (var dataShard = 0; dataShard < k; ++dataShard) {
          if (shardPresent[dataShard])
            continue;

          var target = shards[dataShard];
          Array.Clear(target, 0, shardLength);
          for (var i = 0; i < k; ++i)
            GaloisField256.MultiplyAndAddRegion(decodeMatrix[dataShard][i], survivors[i], 0, target, 0, shardLength);

          shardPresent[dataShard] = true;
        }
      }

      // re-encode any missing parity shards from the now-complete data
      for (var parityShard = 0; parityShard < this.ParityShards; ++parityShard) {
        if (shardPresent[k + parityShard])
          continue;

        var target = shards[k + parityShard];
        Array.Clear(target, 0, shardLength);
        var coefficients = this._parityRows[parityShard];
        for (var column = 0; column < k; ++column)
          GaloisField256.MultiplyAndAddRegion(coefficients[column], shards[column], 0, target, 0, shardLength);

        shardPresent[k + parityShard] = true;
      }

      return true;
    }

    /// <summary>
    /// Returns row <paramref name="shardIndex"/> of the systematic coding matrix:
    /// identity for data shards, parity coefficients below.
    /// </summary>
    private byte[] _GetCodingRow(int shardIndex) {
      var k = this.DataShards;
      if (shardIndex >= k)
        return this._parityRows[shardIndex - k];

      var result = new byte[k];
      result[shardIndex] = 1;
      return result;
    }

  }
}
