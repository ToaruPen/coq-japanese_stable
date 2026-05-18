using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class DimensionManagerGeneratedNameTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
    }

    [Test]
    public void TranslateExpandedText_TranslatesWithoutTouchingCultSymbolNumbers()
    {
        var dimensionName = DimensionManagerGeneratedNameTranslationPatch.TranslateExpandedText("realm of crimson");
        var symbol = DimensionManagerGeneratedNameTranslationPatch.TranslateExpandedText("8756");

        Assert.Multiple(() =>
        {
            Assert.That(dimensionName, Is.EqualTo("crimsonの領域"));
            Assert.That(symbol, Is.EqualTo("8756"));
            Assert.That(HitCount("ExpandString"), Is.EqualTo(1));
        });
    }

    [Test]
    public void Postfix_TranslatesReturnedPsychicFactionFields()
    {
        var faction = new DummyPsychicFaction
        {
            cultForm = "the Cult Of *CultSymbol*",
            dimensionName = "the Realm Of Crimson",
        };

        DimensionManagerGeneratedNameTranslationPatch.Postfix(faction);

        Assert.Multiple(() =>
        {
            Assert.That(faction.cultForm, Is.EqualTo("*CultSymbol*のカルト"));
            Assert.That(faction.dimensionName, Is.EqualTo("Crimsonの領域"));
            Assert.That(HitCount("CultForm"), Is.EqualTo(1));
            Assert.That(HitCount("DimensionName"), Is.EqualTo(1));
        });
    }

    [Test]
    public void ExtraDimensionPostfix_TranslatesGeneratedDimensionListNames()
    {
        var manager = new DummyDimensionManager();
        manager.ExtraDimensions.Add(new DummyExtraDimension { Name = "the Void Of *DimensionSymbol*" });

        DimensionManagerExtraDimensionNameTranslationPatch.Postfix(manager);

        Assert.Multiple(() =>
        {
            Assert.That(manager.ExtraDimensions[0].Name, Is.EqualTo("*DimensionSymbol*の虚空"));
            Assert.That(HitCount("ExtraDimensionName"), Is.EqualTo(1));
        });
    }

    private static int HitCount(string route) =>
        DynamicTextObservability.GetRouteFamilyHitCountForTests(
            nameof(DimensionManagerGeneratedNameTranslationPatch),
            nameof(DimensionManagerGeneratedNameTranslationPatch) + "." + route);

    private sealed class DummyPsychicFaction
    {
        public string cultForm = string.Empty;

        public string dimensionName = string.Empty;
    }

    private sealed class DummyDimensionManager
    {
        public List<DummyExtraDimension> ExtraDimensions { get; } = [];
    }

    private sealed class DummyExtraDimension
    {
        public string Name = string.Empty;
    }
}
