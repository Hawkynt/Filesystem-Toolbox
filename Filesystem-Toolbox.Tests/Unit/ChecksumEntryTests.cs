using Filesystem_Toolbox.Core.Integrity;

namespace Filesystem_Toolbox.Tests.Unit;

[TestFixture]
[Category("Unit")]
public class ChecksumEntryTests {

  private const string _HASH_A = "q83vEjRWeJA=";
  private const string _HASH_B = "ZUdF3q83vEg=";

  [Test]
  public void Given_LegacyTwoFieldValue_When_Parsing_Then_SizeAndHashAreReadAndMtimeIsUnknown() {
    var parsed = ChecksumEntry.TryParse($"12345:{_HASH_A}", out var entry);

    Assert.Multiple(() => {
      Assert.That(parsed, Is.True);
      Assert.That(entry.Size, Is.EqualTo(12345));
      Assert.That(entry.ModificationTimeTicks, Is.Null);
      Assert.That(entry.HashBase64, Is.EqualTo(_HASH_A));
    });
  }

  [Test]
  public void Given_ThreeFieldValue_When_Parsing_Then_AllFieldsAreRead() {
    var parsed = ChecksumEntry.TryParse($"42:638000000000000000:{_HASH_A}", out var entry);

    Assert.Multiple(() => {
      Assert.That(parsed, Is.True);
      Assert.That(entry.Size, Is.EqualTo(42));
      Assert.That(entry.ModificationTimeTicks, Is.EqualTo(638000000000000000));
      Assert.That(entry.HashBase64, Is.EqualTo(_HASH_A));
    });
  }

  [TestCase(null)]
  [TestCase("")]
  [TestCase("   ")]
  [TestCase("justonefield")]
  [TestCase("a:b:c:d")]
  [TestCase("notanumber:hash")]
  [TestCase("1:notanumber:hash")]
  public void Given_MalformedValue_When_Parsing_Then_FalseIsReturned(string? text)
    => Assert.That(ChecksumEntry.TryParse(text!, out _), Is.False);

  [Test]
  public void Given_Entry_When_Serializing_Then_ItParsesBackIdentically() {
    var original = new ChecksumEntry(99, 1234567890, _HASH_A);

    var parsed = ChecksumEntry.TryParse(original.ToString(), out var roundTripped);

    Assert.Multiple(() => {
      Assert.That(parsed, Is.True);
      Assert.That(roundTripped.Size, Is.EqualTo(original.Size));
      Assert.That(roundTripped.ModificationTimeTicks, Is.EqualTo(original.ModificationTimeTicks));
      Assert.That(roundTripped.HashBase64, Is.EqualTo(original.HashBase64));
    });
  }

  [Test]
  public void Given_SameContentDifferentTimestamps_When_ComparingContent_Then_TheyAreEqual() {
    var a = new ChecksumEntry(10, 111, _HASH_A);
    var b = new ChecksumEntry(10, 222, _HASH_A);

    Assert.That(a.ContentEquals(b), Is.True);
  }

  [TestCase(10L, 11L, Description = "size differs")]
  public void Given_DifferentContent_When_ComparingContent_Then_TheyAreNotEqual(long sizeA, long sizeB) {
    Assert.Multiple(() => {
      Assert.That(new ChecksumEntry(sizeA, 1, _HASH_A).ContentEquals(new ChecksumEntry(sizeB, 1, _HASH_A)), Is.False);
      Assert.That(new ChecksumEntry(10, 1, _HASH_A).ContentEquals(new ChecksumEntry(10, 1, _HASH_B)), Is.False);
    });
  }

  [Test]
  public void Given_UnknownLegacyMtime_When_ComparingMetadata_Then_ItCountsAsUnchanged() {

    // conservative classification: a legacy entry without timestamp must classify a
    // hash mismatch as bit rot (surfacing for review) rather than a silent edit
    var legacy = new ChecksumEntry(10, null, _HASH_A);
    var current = new ChecksumEntry(10, 999, _HASH_B);

    Assert.That(current.MetadataEquals(legacy), Is.True);
  }

  [Test]
  public void Given_ChangedMtime_When_ComparingMetadata_Then_TheyDiffer() {
    var stored = new ChecksumEntry(10, 111, _HASH_A);
    var current = new ChecksumEntry(10, 222, _HASH_B);

    Assert.That(current.MetadataEquals(stored), Is.False);
  }

}
