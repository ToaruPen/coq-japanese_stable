using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class SettlementFarmNameTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void Postfix_TranslatesGeneratedFarmName()
    {
        var result = "the Shire of Urist";

        SettlementFarmNameTranslationPatch.Postfix(ref result);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("Uristの村郡"));
            Assert.That(HitCount(), Is.EqualTo(1));
        });
    }

    [Test]
    public void Postfix_StripsDirectMarkerWithoutObservabilityHit()
    {
        var result = MessageFrameTranslator.DirectTranslationMarker + "Urist Farm";

        SettlementFarmNameTranslationPatch.Postfix(ref result);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo("Urist Farm"));
            Assert.That(HitCount(), Is.Zero);
        });
    }

    private static int HitCount() =>
        DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(SettlementFarmNameTranslationPatch),
            nameof(SettlementFarmNameTranslationPatch) + ".GenerateFarmName");
}
