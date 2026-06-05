using Filesystem_Toolbox.Core.Dedup;

namespace Filesystem_Toolbox.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class BufferPoolTests {

  [Test]
  public void Given_Pool_When_Renting_Then_BufferHasConfiguredSize() {
    var pool = new BufferPool(1024);

    using var rented = pool.Use();

    Assert.Multiple(() => {
      Assert.That(rented.Buffer, Has.Length.EqualTo(1024));
      Assert.That(rented.Length, Is.EqualTo(1024));
    });
  }

  [Test]
  public void Given_ReturnedBuffer_When_RentingAgain_Then_SameArrayIsReused() {
    var pool = new BufferPool(64);
    byte[] first;
    using (var rented = pool.Use())
      first = rented.Buffer;

    using var second = pool.Use();

    Assert.That(second.Buffer, Is.SameAs(first));
  }

  [Test]
  public void Given_DisposedRental_When_AccessingBuffer_Then_ObjectDisposedExceptionIsThrown() {
    var pool = new BufferPool(64);
    var rented = pool.Use();
    rented.Dispose();

    Assert.That(() => rented.Buffer, Throws.InstanceOf<ObjectDisposedException>());
  }

  [TestCase(0)]
  [TestCase(-1)]
  public void Given_InvalidBufferSize_When_Constructing_Then_ArgumentOutOfRangeExceptionIsThrown(int size)
    => Assert.That(() => new BufferPool(size), Throws.InstanceOf<ArgumentOutOfRangeException>());

  [Test]
  public void Given_TwoConcurrentRentals_When_Renting_Then_DistinctBuffersAreHandedOut() {
    var pool = new BufferPool(64);

    using var first = pool.Use();
    using var second = pool.Use();

    Assert.That(first.Buffer, Is.Not.SameAs(second.Buffer));
  }

}
