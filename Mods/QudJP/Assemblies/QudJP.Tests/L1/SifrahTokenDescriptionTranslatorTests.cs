using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class SifrahTokenDescriptionTranslatorTests
{
    [TestCase("use liquid", "液体を使う", "UseLiquid")]
    [TestCase("use water", "waterを使う", "UseNamedLiquid")]
    [TestCase("sacrifice a point of an attribute", "能力値を1ポイント捧げる", "SacrificeGenericAttribute")]
    [TestCase("sacrifice a point of Strength", "Strengthを1ポイント捧げる", "SacrificeNamedAttribute")]
    [TestCase("invoke Shekhinah", "Shekhinahを呼び出す", "InvokeBeing")]
    [TestCase(
        "invoke Shekhinah, in the manner of {{W|Mechanimists}}",
        "{{W|Mechanimists}}流にShekhinahを呼び出す",
        "InvokeBeingManner")]
    [TestCase("crack a joke", "冗談を言う", "Exact.CrackAJoke")]
    [TestCase("accept becoming {{C|dazed}}", "{{C|朦朧}}状態になることを受け入れる", "Exact.AcceptDazed")]
    [TestCase("display a merchant's token", "商人のしるしを見せる", "Exact.DisplayMerchantsToken")]
    [TestCase("apply knowledge of this artifact's manufacture", "このアーティファクトの製造知識を使う", "Exact.ApplyCreationKnowledge")]
    public void TryTranslateDescription_TranslatesCoveredSifrahTokenDescriptions(
        string source,
        string expected,
        string expectedDetail)
    {
        var translated = SifrahTokenDescriptionTranslator.TryTranslateDescription(source, out var result, out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(detail, Is.EqualTo(expectedDetail));
        });
    }

    [Test]
    public void TryTranslateDescription_PreservesWholeSourceColorWrapper()
    {
        var translated = SifrahTokenDescriptionTranslator.TryTranslateDescription(
            "{{y|sacrifice a point of Strength}}",
            out var result,
            out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{y|Strengthを1ポイント捧げる}}"));
            Assert.That(detail, Is.EqualTo("SacrificeNamedAttribute"));
        });
    }

    [TestCase("")]
    [TestCase("unsupported token description")]
    public void TryTranslateDescription_LeavesUnsupportedAndMarkedTextUnchanged(string source)
    {
        var translated = SifrahTokenDescriptionTranslator.TryTranslateDescription(source, out var result, out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source));
            Assert.That(detail, Is.Empty);
        });
    }

    [Test]
    public void TryTranslateDescription_StripsDirectMarkedTextWithoutRetranslating()
    {
        var translated = SifrahTokenDescriptionTranslator.TryTranslateDescription(
            "\u0001use liquid",
            out var result,
            out var detail);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("use liquid"));
            Assert.That(detail, Is.Empty);
        });
    }
}
