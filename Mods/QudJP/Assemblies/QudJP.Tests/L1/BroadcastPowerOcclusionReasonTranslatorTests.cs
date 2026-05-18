using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class BroadcastPowerOcclusionReasonTranslatorTests
{
    [TestCase("orbital debris", "軌道上の残骸")]
    [TestCase("a glass storm", "ガラス嵐")]
    [TestCase("a flock of birds", "鳥の群れ")]
    [TestCase("acid rain", "酸性雨")]
    [TestCase("drift film", "ドリフト膜")]
    [TestCase("an unidentified anomaly", "未確認の異常")]
    public void TryTranslate_TranslatesFiniteOcclusionReasons(string source, string expected)
    {
        var translated = BroadcastPowerOcclusionReasonTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutRetranslating()
    {
        var source = MessageFrameTranslator.DirectTranslationMarker + "orbital debris";

        var translated = BroadcastPowerOcclusionReasonTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("orbital debris"));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("unknown occlusion")]
    public void TryTranslate_LeavesUnknownOrEmptyText(string? source)
    {
        var translated = BroadcastPowerOcclusionReasonTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source ?? string.Empty));
        });
    }
}
