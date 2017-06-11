using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Classes {
  public class TaskQueue : IDisposable {

    #region nested types

    private class QueueItem {
      private readonly Action _task;
      public QueueItem(Action task) {
        if (task == null) throw new ArgumentNullException(nameof(task));

        this._task = task;
      }

      public virtual bool TagEquals(object tag) => false;
      public void Execute() => this._task();
    }

    private class QueueItemWithTag : QueueItem {
      private readonly object _tag;
      public QueueItemWithTag(Action task, object tag) : base(task) {
        this._tag = tag;
      }

      #region Overrides of QueueItem

      public override bool TagEquals(object tag) => ReferenceEquals(tag, this._tag) || Equals(tag, this._tag);

      #endregion
    }

    #endregion

    private const int _TRUE = -1;
    private const int _FALSE = 0;
    private int _workerPresent = _FALSE;
    private readonly ConcurrentQueue<QueueItem> _items = new ConcurrentQueue<QueueItem>();
    private readonly ManualResetEventSlim _allowWorker = new ManualResetEventSlim(true);

    #region IDisposable

    private void _ReleaseUnmanagedResources() {
      this.Clear();
      this._allowWorker.Dispose();
    }

    public void Dispose() {
      this._ReleaseUnmanagedResources();
      GC.SuppressFinalize(this);
    }

    ~TaskQueue() {
      this._ReleaseUnmanagedResources();
    }

    #endregion

    public bool RequeueOnException { get; set; }

    public void Clear() {
      try {
        this._PauseWorker();

        // clear queue
        QueueItem dummy;
        while (this._items.TryDequeue(out dummy)) {
          ;
        }
      } finally {
        this._ResumeWorker();
      }
    }

    public void Enqueue(Action task) => this._Enqueue(new QueueItem(task));
    public void Enqueue(Action task, object tag) => this._Enqueue(new QueueItemWithTag(task, tag));
    public void Enqueue<TTag>(Action task, TTag tag) => this._Enqueue(new QueueItemWithTag(task, tag));

    private void _PauseWorker() => this._allowWorker.Reset();
    private void _ResumeWorker() => this._allowWorker.Set();

    private void _Enqueue(QueueItem item) {
      this._items.Enqueue(item);
      if (Interlocked.CompareExchange(ref this._workerPresent, _TRUE, _FALSE) != _FALSE)
        return;

      Action task = this._QueueWorker;
      task.BeginInvoke(task.EndInvoke, null);
    }

    public void DequeueByTag<TTag>(TTag tag) => this._DequeueByTag(tag);
    public void DequeueByTag(object tag) => this._DequeueByTag(tag);

    private void _DequeueByTag(object tag) {
      try {
        this._PauseWorker();

        // first dequeue all items and mark the ones that don't match
        QueueItem item;
        var list = new List<QueueItem>(this._items.Count);
        while (this._items.TryDequeue(out item))
          if (!item.TagEquals(tag))
            list.Add(item);

        // requeue all items that we marked
        foreach (var item2 in list)
          this._items.Enqueue(item2);

      } finally {
        this._ResumeWorker();
      }
    }

    private void _QueueWorker() {
      while (true) {
        this._allowWorker.Wait();
        QueueItem currentItem;
        if (!this._items.TryDequeue(out currentItem)) {
          Interlocked.CompareExchange(ref this._workerPresent, _FALSE, _TRUE);
          return;
        }

        if (this.RequeueOnException) {
          try {
            currentItem.Execute();
          } catch (Exception) {
            this._Enqueue(currentItem);
          }
        } else
          currentItem.Execute();
      }
    }

  }
}
