using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class MessageLogPatchTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [Test]
    public void Prefix_ObservationOnly_DoesNotTranslateMessage()
    {
        var message = "You hit the bear.";
        MessageLogPatch.Prefix(ref message);
        Assert.That(message, Is.EqualTo("You hit the bear."));
    }

    [Test]
    public void Prefix_ObservationOnly_LogsUnclaimed()
    {
        var message = "You hit the bear.";
        var originalMessage = message;
        MessageLogPatch.Prefix(ref message);
        var hitCount = SinkObservation.GetHitCountForTests(
            nameof(MessageLogPatch), nameof(MessageLogPatch), SinkObservation.ObservationOnlyDetail, originalMessage, originalMessage);
        Assert.That(hitCount, Is.GreaterThan(0));
    }

    [Test]
    public void Prefix_DirectMarker_StillStripped()
    {
        var message = "\u0001すでに翻訳済みテキスト";
        var result = MessageLogPatch.Prefix(ref message);
        Assert.That(message, Is.EqualTo("すでに翻訳済みテキスト"));
        Assert.That(result, Is.True);
    }
}
