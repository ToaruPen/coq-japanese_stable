using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
public sealed class CyberneticsBehaviorDescriptionTranslationPatchTests
{
    [SetUp]
    public void SetUp()
    {
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        DynamicTextObservability.ResetForTests();
    }

    [TestCase(
        "You gain access to every schematic of low tier ammo and energy cells.",
        "下位の弾薬とエネルギーセルの全設計図にアクセスできる。")]
    [TestCase(
        "You gain access to every schematic of mid tier pistols.",
        "中位のピストルの全設計図にアクセスできる。")]
    [TestCase(
        "You gain access to every schematic of high tier heavy weapons.",
        "上位の重火器の全設計図にアクセスできる。")]
    public void Postfix_TranslatesSchemasoftGeneratedBehaviorDescription(string source, string expected)
    {
        string? result = source;

        CyberneticsBehaviorDescriptionTranslationPatch.Postfix(ref result);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests(
                    nameof(CyberneticsBehaviorDescriptionTranslationPatch),
                    "CyberneticsBehaviorDescription.Schemasoft"),
                Is.EqualTo(1));
        });
    }

    [Test]
    public void Postfix_TranslatesSchemasoftAddOnLine_WithoutChangingLocalizedBaseDescription()
    {
        string? result = "単一の遺物分類の全設計図にアクセスできる。\n"
            + "You gain access to every schematic of high tier grenades.";

        CyberneticsBehaviorDescriptionTranslationPatch.Postfix(ref result);

        Assert.That(
            result,
            Is.EqualTo("単一の遺物分類の全設計図にアクセスできる。\n上位のグレネードの全設計図にアクセスできる。"));
    }

    [TestCase("You gain access to every schematic of unknown tier pistols.")]
    [TestCase("You gain access to every schematic of high tier unknown category.")]
    [TestCase("単一の遺物分類の全設計図にアクセスできる。")]
    public void Postfix_LeavesUnknownOrAlreadyLocalizedBehaviorDescriptionUnchanged(string source)
    {
        string? result = source;

        CyberneticsBehaviorDescriptionTranslationPatch.Postfix(ref result);

        Assert.That(result, Is.EqualTo(source));
    }
}
