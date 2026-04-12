namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class SinkObservationTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [Test]
    public void LogUnclaimed_LogsPowerOfTwoHits()
    {
        var output = TestTraceHelper.CaptureTrace(() =>
        {
            SinkObservation.LogUnclaimed(
                "PopupTranslationPatch",
                "PopupTranslationPatch",
                "UITextSkinTranslationPatch",
                "{{R|Unknown text}}",
                "Unknown text");
            SinkObservation.LogUnclaimed(
                "PopupTranslationPatch",
                "PopupTranslationPatch",
                "UITextSkinTranslationPatch",
                "{{R|Unknown text}}",
                "Unknown text");
            SinkObservation.LogUnclaimed(
                "PopupTranslationPatch",
                "PopupTranslationPatch",
                "UITextSkinTranslationPatch",
                "{{R|Unknown text}}",
                "Unknown text");
            SinkObservation.LogUnclaimed(
                "PopupTranslationPatch",
                "PopupTranslationPatch",
                "UITextSkinTranslationPatch",
                "{{R|Unknown text}}",
                "Unknown text");
        });

        Assert.Multiple(() =>
        {
            Assert.That(CountOccurrences(output, "SinkObserve/v1"), Is.EqualTo(3));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    "PopupTranslationPatch",
                    "PopupTranslationPatch",
                    "UITextSkinTranslationPatch",
                    "{{R|Unknown text}}",
                    "Unknown text"),
                Is.EqualTo(4));
        });
    }

    [Test]
    public void LogUnclaimed_UsesExpectedFormat()
    {
        var output = TestTraceHelper.CaptureTrace(() =>
            SinkObservation.LogUnclaimed(
                "UITextSkinTranslationPatch",
                "PopupTranslationPatch > title",
                "Translator",
                "Line 1\nLine 2",
                "Line 1\nLine 2"));

        Assert.That(
            output,
            Does.Contain(
                "[QudJP] SinkObserve/v1: sink='UITextSkinTranslationPatch' route='PopupTranslationPatch' detail='Translator' source='Line 1\\nLine 2' stripped='Line 1\\nLine 2'; route=PopupTranslationPatch; family=sink_observe; template_id=<missing>; payload_mode=full; payload_excerpt=Line 1\\\\nLine 2; payload_sha256=<missing>"));
    }

    [Test]
    public void LogUnclaimed_UsesPrefixHashStructuredSuffix_WhenPayloadExceedsLimit()
    {
        const string sink = "MessageLog";
        const string route = "EmitMessage";
        var longPayload = new string('z', 600);
        var expectedExcerpt = new string('z', 512);
        var expectedHash = ComputeSha256Hex(longPayload);

        var output = TestTraceHelper.CaptureTrace(() =>
            SinkObservation.LogUnclaimed(sink, route, SinkObservation.ObservationOnlyDetail, longPayload, longPayload));

        Assert.That(
            output,
            Does.Contain(
                "; route=EmitMessage; family=sink_observe; template_id=<missing>; payload_mode=prefix_hash; payload_excerpt="
                + expectedExcerpt
                + "; payload_sha256="
                + expectedHash));
    }

    [Test]
    public void LogUnclaimed_SuppressesNestedSinkChains()
    {
        var output = TestTraceHelper.CaptureTrace(() =>
        {
            SinkObservation.LogUnclaimed(
                "PopupTranslationPatch",
                "PopupTranslationPatch",
                "UITextSkinTranslationPatch",
                "{{R|Unknown text}}",
                "Unknown text");
            using var _ = SinkObservation.PushSuppression(true);
            SinkObservation.LogUnclaimed(
                "UITextSkinTranslationPatch",
                "PopupTranslationPatch",
                "Translator",
                "{{R|Unknown text}}",
                "Unknown text");
        });

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("sink='PopupTranslationPatch'"));
            Assert.That(output, Does.Not.Contain("sink='UITextSkinTranslationPatch'"));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    "PopupTranslationPatch",
                    "PopupTranslationPatch",
                    "UITextSkinTranslationPatch",
                    "{{R|Unknown text}}",
                    "Unknown text"),
                Is.EqualTo(1));
            Assert.That(
                SinkObservation.GetHitCountForTests(
                    "UITextSkinTranslationPatch",
                    "PopupTranslationPatch",
                    "Translator",
                    "{{R|Unknown text}}",
                    "Unknown text"),
                Is.EqualTo(0));
        });
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string ComputeSha256Hex(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
