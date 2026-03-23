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
                "[QudJP] SinkObserve/v1: sink='UITextSkinTranslationPatch' route='PopupTranslationPatch' detail='Translator' source='Line 1\\nLine 2' stripped='Line 1\\nLine 2'"));
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
}
