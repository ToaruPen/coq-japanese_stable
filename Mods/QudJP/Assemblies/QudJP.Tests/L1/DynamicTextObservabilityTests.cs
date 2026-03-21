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
}
