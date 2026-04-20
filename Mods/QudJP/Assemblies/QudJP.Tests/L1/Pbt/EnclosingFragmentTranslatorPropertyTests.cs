using FsCheck.Fluent;

using FsCheckProperty = FsCheck.Property;

using QudJP.Patches;

namespace QudJP.Tests.L1.Pbt;

[TestFixture]
[Category("L1")]
public sealed class EnclosingFragmentTranslatorPropertyTests
{
    private const string ReplaySeed = "531246879,97531";

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(EnclosingFragmentTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryTranslatePopupMessage_PreservesExtricateTranslation(EnclosingExtricateCase sample)
    {
        var translated = EnclosingFragmentTranslator.TryTranslatePopupMessage(
            sample.Source,
            nameof(EnclosingFragmentTranslatorPropertyTests),
            "Enclosing",
            out var actual);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(actual, Is.EqualTo(sample.ExpectedTranslated));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(EnclosingFragmentTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryTranslatePopupMessage_LeavesDirectMarkedTextUntouched(EnclosingPassthroughCase sample)
    {
        var translated = EnclosingFragmentTranslator.TryTranslatePopupMessage(
            sample.Source,
            nameof(EnclosingFragmentTranslatorPropertyTests),
            "Enclosing",
            out var actual);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(actual, Is.EqualTo(sample.Source));
        });

        return true.ToProperty();
    }
}
