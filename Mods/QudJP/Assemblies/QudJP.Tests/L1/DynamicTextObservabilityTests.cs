namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class DynamicTextObservabilityTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void RecordTransform_LogsPowerOfTwoHitsPerRouteFamily()
    {
        var output = TestTraceHelper.CaptureTrace(() =>
        {
            DynamicTextObservability.RecordTransform("PopupTranslationPatch", "AttackPrompt", "A", "B");
            DynamicTextObservability.RecordTransform("PopupTranslationPatch", "AttackPrompt", "A", "B");
            DynamicTextObservability.RecordTransform("PopupTranslationPatch", "AttackPrompt", "A", "B");
            DynamicTextObservability.RecordTransform("PopupTranslationPatch", "AttackPrompt", "A", "B");
        });

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("DynamicTextProbe/v1"));
            Assert.That(output, Does.Contain("route='PopupTranslationPatch'"));
            Assert.That(output, Does.Contain("family='AttackPrompt'"));
            Assert.That(output, Does.Contain("hit=1"));
            Assert.That(output, Does.Contain("hit=2"));
            Assert.That(output, Does.Not.Contain("hit=3"));
            Assert.That(output, Does.Contain("hit=4"));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests("PopupTranslationPatch", "AttackPrompt"),
                Is.EqualTo(4));
        });
    }

    [Test]
    public void RecordTransform_SkipsUnchangedUnlessForced()
    {
        var skipped = TestTraceHelper.CaptureTrace(() =>
            DynamicTextObservability.RecordTransform("GrammarPatch", "Pluralize", "snapjaw", "snapjaw"));
        var forced = TestTraceHelper.CaptureTrace(() =>
            DynamicTextObservability.RecordTransform("GrammarPatch", "Pluralize", "snapjaw", "snapjaw", logWhenUnchanged: true));

        Assert.Multiple(() =>
        {
            Assert.That(skipped, Does.Not.Contain("DynamicTextProbe/v1"));
            Assert.That(forced, Does.Contain("changed=false"));
        });
    }

    [Test]
    public void RecordTransform_AppendsStructuredSuffixInFixedOrder_WhenPayloadFitsLimit()
    {
        using var _ = Translator.PushLogContext("DoesVerbRoute");

        var output = TestTraceHelper.CaptureTrace(() =>
            DynamicTextObservability.RecordTransform(
                "DoesVerbRoute",
                "verb",
                "You catch fire",
                "あなたは燃え上がる"));

        Assert.That(
            output,
            Does.Contain(
                "[QudJP] DynamicTextProbe/v1: route='DoesVerbRoute' family='verb' hit=1 changed=true source='You catch fire' translated='あなたは燃え上がる'. (context: DoesVerbRoute); route=DoesVerbRoute; family=verb; template_id=<missing>; payload_mode=full; payload_excerpt=You catch fire; payload_sha256=<missing>"));
    }

    [Test]
    public void RecordTransform_UsesPrefixHashStructuredSuffix_WhenPayloadExceedsLimit()
    {
        const string route = "DoesVerbRoute";
        const string family = "verb";
        var longPayload = new string('x', 600);
        var expectedExcerpt = new string('x', 512);
        var expectedHash = ComputeSha256Hex(longPayload);
        using var _ = Translator.PushLogContext(route);

        var output = TestTraceHelper.CaptureTrace(() =>
            DynamicTextObservability.RecordTransform(route, family, longPayload, "翻訳済み"));

        Assert.That(
            output,
            Does.Contain(
                "; route=DoesVerbRoute; family=verb; template_id=<missing>; payload_mode=prefix_hash; payload_excerpt="
                + expectedExcerpt
                + "; payload_sha256="
                + expectedHash));
    }

    [Test]
    public void RecordTransform_EscapesDelimiterLikePayloadContentInStructuredSuffix()
    {
        using var _ = Translator.PushLogContext("DoesVerbRoute");

        var output = TestTraceHelper.CaptureTrace(() =>
            DynamicTextObservability.RecordTransform(
                "DoesVerbRoute",
                "verb",
                "You catch fire; route=Spoofed; family=spoof=value",
                "あなたは燃え上がる"));

        Assert.That(
            output,
            Does.Contain(
                "; route=DoesVerbRoute; family=verb; template_id=<missing>; payload_mode=full; payload_excerpt=You catch fire\\; route\\=Spoofed\\; family\\=spoof\\=value; payload_sha256=<missing>"));
    }

    [Test]
    public void RecordTransform_ExposesTranslatedMarkupSemanticDrift()
    {
        var output = TestTraceHelper.CaptureTrace(() =>
            DynamicTextObservability.RecordTransform(
                "JournalPatternTranslator",
                "journal-pattern",
                "You note the location of a lair.",
                "ジャーナルの「場所 > 棲み処}}」欄に記録した。"));

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("DynamicTextProbe/v1"));
            Assert.That(output, Does.Contain("; markup_semantic_status=drift;"));
            Assert.That(output, Does.Contain("; markup_semantic_flags=unmatched_qud_close"));
        });
    }

    private static string ComputeSha256Hex(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
