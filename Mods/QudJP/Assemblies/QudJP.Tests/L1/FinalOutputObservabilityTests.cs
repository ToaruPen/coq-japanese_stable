namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class FinalOutputObservabilityTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        FinalOutputObservability.ResetForTests();
    }

    [Test]
    public void Record_LogsPowerOfTwoHitsPerFinalOutputIdentity()
    {
        var observation = CreateObservation(finalText: "Unknown text");

        var output = TestTraceHelper.CaptureTrace(() =>
        {
            FinalOutputObservability.Record(observation);
            FinalOutputObservability.Record(observation);
            FinalOutputObservability.Record(observation);
            FinalOutputObservability.Record(observation);
        });

        Assert.Multiple(() =>
        {
            Assert.That(CountOccurrences(output, "FinalOutputProbe/v1"), Is.EqualTo(3));
            Assert.That(output, Does.Contain("hit=1"));
            Assert.That(output, Does.Contain("hit=2"));
            Assert.That(output, Does.Not.Contain("hit=3"));
            Assert.That(output, Does.Contain("hit=4"));
            Assert.That(FinalOutputObservability.GetHitCountForTests(observation), Is.EqualTo(4));
        });
    }

    [Test]
    public void Record_UsesExpectedFormatAndFinalTextPayloadIdentity()
    {
        var output = TestTraceHelper.CaptureTrace(() =>
            FinalOutputObservability.Record(
                CreateObservation(
                    sourceText: "{{R|Unknown text}}",
                    strippedText: "Unknown text",
                    translatedText: string.Empty,
                    finalText: "{{R|Unknown text}}")));

        Assert.That(
            output,
            Does.Contain(
                "[QudJP] FinalOutputProbe/v1: sink='UITextSkinTranslationPatch' route='PopupTranslationPatch' detail='ObservationOnly' phase='before_sink' translation_status='sink_unclaimed' markup_status='not_evaluated' direct_marker_status='not_evaluated' hit=1 source='{{R|Unknown text}}' stripped='Unknown text' translated='' final='{{R|Unknown text}}'; route=PopupTranslationPatch; family=final_output; template_id=<missing>; payload_mode=full; payload_excerpt={{R|Unknown text}}; payload_sha256=<missing>; sink=UITextSkinTranslationPatch; detail=ObservationOnly; phase=before_sink; translation_status=sink_unclaimed; markup_status=not_evaluated; direct_marker_status=not_evaluated; source_text_sample={{R|Unknown text}}; stripped_text_sample=Unknown text; translated_text_sample=; final_text_sample={{R|Unknown text}}"));
    }

    [Test]
    public void Record_UsesPrefixHashStructuredSuffix_WhenFinalTextExceedsLimit()
    {
        var longFinal = new string('z', 600);
        var expectedExcerpt = new string('z', 512);
        var expectedHash = ComputeSha256Hex(longFinal);

        var output = TestTraceHelper.CaptureTrace(() =>
            FinalOutputObservability.Record(CreateObservation(finalText: longFinal)));

        Assert.That(
            output,
            Does.Contain(
                "; route=PopupTranslationPatch; family=final_output; template_id=<missing>; payload_mode=prefix_hash; payload_excerpt="
                + expectedExcerpt
                + "; payload_sha256="
                + expectedHash));
    }

    [Test]
    public void Record_EscapesDelimiterLikeStructuredValues()
    {
        var output = TestTraceHelper.CaptureTrace(() =>
            FinalOutputObservability.Record(
                CreateObservation(
                    sourceText: "source; route=Spoofed; family=spoof=value",
                    strippedText: "stripped; detail=Spoofed",
                    translatedText: "translated; phase=Spoofed",
                    finalText: "final; sink=Spoofed; status=value")));

        Assert.That(
            output,
            Does.Contain(
                "payload_excerpt=final\\; sink\\=Spoofed\\; status\\=value; payload_sha256=<missing>; sink=UITextSkinTranslationPatch; detail=ObservationOnly; phase=before_sink; translation_status=sink_unclaimed; markup_status=not_evaluated; direct_marker_status=not_evaluated; source_text_sample=source\\; route\\=Spoofed\\; family\\=spoof\\=value; stripped_text_sample=stripped\\; detail\\=Spoofed; translated_text_sample=translated\\; phase\\=Spoofed; final_text_sample=final\\; sink\\=Spoofed\\; status\\=value"));
    }

    [Test]
    public void Record_EscapesApostrophesInQuotedPrefix()
    {
        var output = TestTraceHelper.CaptureTrace(() =>
            FinalOutputObservability.Record(
                CreateObservation(
                    sourceText: "You don't penetrate the snapjaw.",
                    strippedText: "You don't penetrate the snapjaw.",
                    finalText: "You don't penetrate the snapjaw.")));

        Assert.That(
            output,
            Does.Contain(
                "source='You don\\'t penetrate the snapjaw.' stripped='You don\\'t penetrate the snapjaw.'"));
        Assert.That(output, Does.Contain("final='You don\\'t penetrate the snapjaw.'"));
        Assert.That(output, Does.Contain("final_text_sample=You don't penetrate the snapjaw."));
    }

    [Test]
    public void Record_DoesNotMergeCounterKeys_WhenFieldsContainContextSeparator()
    {
        var first = CreateObservation(sourceText: "A > B", strippedText: "C", finalText: "same");
        var second = CreateObservation(sourceText: "A", strippedText: "B > C", finalText: "same");

        var output = TestTraceHelper.CaptureTrace(() =>
        {
            FinalOutputObservability.Record(first);
            FinalOutputObservability.Record(second);
        });

        Assert.Multiple(() =>
        {
            Assert.That(FinalOutputObservability.GetHitCountForTests(first), Is.EqualTo(1));
            Assert.That(FinalOutputObservability.GetHitCountForTests(second), Is.EqualTo(1));
            Assert.That(CountOccurrences(output, "hit=1"), Is.EqualTo(2));
            Assert.That(output, Does.Not.Contain("hit=2"));
        });
    }

    [Test]
    public void RecordSinkUnclaimed_ComputesMatchedMarkupStatus()
    {
        var output = TestTraceHelper.CaptureTrace(() =>
            FinalOutputObservability.RecordSinkUnclaimed(
                "UITextSkinTranslationPatch",
                "PopupTranslationPatch",
                "ObservationOnly",
                "{{R|Unknown text}}",
                "Unknown text"));

        Assert.That(output, Does.Contain("translation_status='sink_unclaimed'"));
        Assert.That(output, Does.Contain("markup_status='matched'"));
        Assert.That(output, Does.Contain("direct_marker_status='absent'"));
    }

    [Test]
    public void RecordDirectMarker_RecordsTranslatedFinalOutputProvenance()
    {
        var output = TestTraceHelper.CaptureTrace(() =>
            FinalOutputObservability.RecordDirectMarker(
                "MessageLogPatch",
                "MessageLogPatch",
                "DirectMarker",
                "\u0001{{G|あなたは命中した。}}",
                "{{G|あなたは命中した。}}"));

        Assert.That(output, Does.Contain("translation_status='direct_marker'"));
        Assert.That(output, Does.Contain("direct_marker_status='present'"));
        Assert.That(output, Does.Contain("markup_status='matched'"));
        Assert.That(output, Does.Contain("translated='{{G|あなたは命中した。}}' final='{{G|あなたは命中した。}}'"));
    }

    [TestCase("plain", "plain", "no_markup")]
    [TestCase("{{R|source}}", "{{R|source}}", "matched")]
    [TestCase("{{R|source}}", "source", "source_only")]
    [TestCase("source", "{{R|source}}", "final_only")]
    [TestCase("{{R|source}}", "{{G|source}}", "mismatch")]
    public void ComputeMarkupStatusForTests_ClassifiesSourceFinalSignatures(
        string source,
        string final,
        string expectedStatus)
    {
        Assert.That(FinalOutputObservability.ComputeMarkupStatusForTests(source, final), Is.EqualTo(expectedStatus));
    }

    private static FinalOutputObservation CreateObservation(
        string sourceText = "Unknown text",
        string strippedText = "Unknown text",
        string translatedText = "",
        string finalText = "Unknown text")
    {
        return new FinalOutputObservation(
            "UITextSkinTranslationPatch",
            "PopupTranslationPatch",
            "ObservationOnly",
            "before_sink",
            "sink_unclaimed",
            "not_evaluated",
            "not_evaluated",
            sourceText,
            strippedText,
            translatedText,
            finalText);
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
