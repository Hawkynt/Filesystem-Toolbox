using Filesystem_Toolbox.Core.Scheduling;

namespace Filesystem_Toolbox.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class TaskQueueTests {

  private static readonly TimeSpan _TIMEOUT = TimeSpan.FromSeconds(10);

  [Test]
  public void Given_EnqueuedTask_When_QueueRuns_Then_TaskIsExecuted() {
    using var queue = new TaskQueue();
    using var executed = new ManualResetEventSlim(false);

    queue.Enqueue(executed.Set);

    Assert.That(executed.Wait(_TIMEOUT), Is.True, "task was never executed");
  }

  [Test]
  public void Given_MultipleTasks_When_QueueRuns_Then_TasksExecuteInEnqueueOrder() {
    using var queue = new TaskQueue();
    using var done = new ManualResetEventSlim(false);
    var order = new List<int>();

    queue.Enqueue(() => order.Add(1));
    queue.Enqueue(() => order.Add(2));
    queue.Enqueue(() => order.Add(3));
    queue.Enqueue(done.Set);

    Assert.That(done.Wait(_TIMEOUT), Is.True, "queue never drained");
    Assert.That(order, Is.EqualTo(new[] { 1, 2, 3 }));
  }

  [Test]
  public void Given_TaggedTask_When_DequeuedByTagBeforeExecution_Then_TaskNeverRuns() {
    using var queue = new TaskQueue();
    using var blockerStarted = new ManualResetEventSlim(false);
    using var releaseBlocker = new ManualResetEventSlim(false);
    using var done = new ManualResetEventSlim(false);
    var taggedRan = false;

    queue.Enqueue(() => {
      blockerStarted.Set();
      releaseBlocker.Wait(_TIMEOUT);
    });
    Assert.That(blockerStarted.Wait(_TIMEOUT), Is.True, "blocker never started");

    queue.Enqueue(() => taggedRan = true, "the-tag");
    queue.DequeueByTag("the-tag");
    queue.Enqueue(done.Set);
    releaseBlocker.Set();

    Assert.That(done.Wait(_TIMEOUT), Is.True, "queue never drained");
    Assert.That(taggedRan, Is.False, "dequeued task was still executed");
  }

  [Test]
  public void Given_RequeueOnException_When_TaskThrowsOnce_Then_TaskIsRetried() {
    using var queue = new TaskQueue { RequeueOnException = true };
    using var succeeded = new ManualResetEventSlim(false);
    var attempts = 0;

    queue.Enqueue(() => {
      if (Interlocked.Increment(ref attempts) == 1)
        throw new InvalidOperationException("flaky");

      succeeded.Set();
    });

    Assert.That(succeeded.Wait(_TIMEOUT), Is.True, "task was never retried");
    Assert.That(attempts, Is.GreaterThanOrEqualTo(2));
  }

  [Test]
  public void Given_NoRequeueOnException_When_TaskThrows_Then_QueueContinuesWithNextTask() {
    using var queue = new TaskQueue { RequeueOnException = false };
    using var done = new ManualResetEventSlim(false);

    queue.Enqueue(() => throw new InvalidOperationException("boom"));
    queue.Enqueue(done.Set);

    Assert.That(done.Wait(_TIMEOUT), Is.True, "queue died after exception");
  }

  [Test]
  public void Given_NullTask_When_Enqueued_Then_ArgumentNullExceptionIsThrown() {
    using var queue = new TaskQueue();

    Assert.That(() => queue.Enqueue(null!), Throws.ArgumentNullException);
  }

}
