using FsCheck.Fluent;

using FsCheckProperty = FsCheck.Property;

namespace QudJP.Tests.L1.Pbt;

[TestFixture]
[Category("L1")]
public sealed class MessageFrameTranslatorPropertyTests
{
    private const string ReplaySeed = "864209753,13579";

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(MessageFrameTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty MarkDirectTranslation_IsIdempotentAndRoundTrips(DirectMarkerText sample)
    {
        var markedOnce = MessageFrameTranslator.MarkDirectTranslation(sample.Value);
        var markedTwice = MessageFrameTranslator.MarkDirectTranslation(markedOnce);
        var stripped = MessageFrameTranslator.TryStripDirectTranslationMarker(markedTwice, out var unmarked);

        Assert.Multiple(() =>
        {
            Assert.That(markedOnce[0], Is.EqualTo(MessageFrameTranslator.DirectTranslationMarker));
            Assert.That(markedTwice, Is.EqualTo(markedOnce));
            Assert.That(stripped, Is.True);
            Assert.That(unmarked, Is.EqualTo(sample.Value));
        });

        return true.ToProperty();
    }

    [FsCheck.NUnit.Property(Arbitrary = new[] { typeof(MessageFrameTranslatorArbitraries) }, MaxTest = 100, Replay = ReplaySeed)]
    public FsCheckProperty TryStripDirectTranslationMarker_LeavesUnmarkedTextUntouched(DirectMarkerText sample)
    {
        var stripped = MessageFrameTranslator.TryStripDirectTranslationMarker(sample.Value, out var unmarked);
        var byRef = sample.Value;
        var strippedByRef = MessageFrameTranslator.TryStripDirectTranslationMarker(ref byRef);

        Assert.Multiple(() =>
        {
            Assert.That(stripped, Is.False);
            Assert.That(unmarked, Is.EqualTo(sample.Value));
            Assert.That(strippedByRef, Is.False);
            Assert.That(byRef, Is.EqualTo(sample.Value));
        });

        return true.ToProperty();
    }
}
