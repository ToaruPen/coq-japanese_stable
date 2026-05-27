using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class SifrahTokenDescriptionTranslatorTests
{
    [SetUp]
    public void SetUp()
    {
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
    }

    [TearDown]
    public void TearDown()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
    }

    [TestCase("use liquid", "液体を使う", "UseLiquid")]
    [TestCase("use water", "水を使う", "UseNamedLiquid")]
    [TestCase("use {{C|10000}} charge from an energy cell", "エネルギーセルから{{C|10000}}チャージを使う", "UseCharge")]
    [TestCase("use {{C|10000}} charge via Electrical Generation", "電気生成で{{C|10000}}チャージを使う", "UseCharge")]
    [TestCase("offer {{C|2000}} charge from an energy cell", "エネルギーセルから{{C|2000}}チャージを差し出す", "OfferCharge")]
    [TestCase("sacrifice a point of an attribute", "能力値を1ポイント捧げる", "SacrificeGenericAttribute")]
    [TestCase("sacrifice a point of Strength", "Strengthを1ポイント捧げる", "SacrificeNamedAttribute")]
    [TestCase("invoke Shekhinah", "Shekhinahを呼び出す", "InvokeBeing")]
    [TestCase("leverage being favored by {{C|the Barathrumites}}", "{{C|バラサラム派（技師団）}}からの好意を利用する", "LeverageFavoredFaction")]
    [TestCase("leverage being loved by {{C|the Mechanimists}}", "{{C|メカニマス教団}}から愛されていることを利用する", "LeverageLovedFaction")]
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
    public void TryTranslateDescription_TranslatesDisplayedAvailabilitySuffixes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                SifrahTokenDescriptionTranslator.TryTranslateDescription(
                    "use {{B|water}} [have {{C|2}} drams]",
                    out var useLiquid,
                    out var useDetail),
                Is.True);
            Assert.That(useLiquid, Is.EqualTo("{{B|水}}を使う [所持: {{C|2}}ドラム]"));
            Assert.That(useDetail, Is.EqualTo("UseNamedLiquid.HaveDrams"));

            Assert.That(
                SifrahTokenDescriptionTranslator.TryTranslateDescription(
                    "share {{B|water}} [have {{C|1}} dram]",
                    out var shareLiquid,
                    out var shareDetail),
                Is.True);
            Assert.That(shareLiquid, Is.EqualTo("{{B|水}}を分かち合う [所持: {{C|1}}ドラム]"));
            Assert.That(shareDetail, Is.EqualTo("ShareNamedLiquid.HaveDrams"));

            Assert.That(
                SifrahTokenDescriptionTranslator.TryTranslateDescription(
                    "tell a secret [have {{C|2}}]",
                    out var secret,
                    out var secretDetail),
                Is.True);
            Assert.That(secret, Is.EqualTo("秘密を話す [所持: {{C|2}}]"));
            Assert.That(secretDetail, Is.EqualTo("HaveCount"));

            Assert.That(
                SifrahTokenDescriptionTranslator.TryTranslateDescription(
                    "use a length of copper wire [have {{C|3}}]",
                    out var copperWire,
                    out var copperWireDetail),
                Is.True);
            Assert.That(copperWire, Is.EqualTo("銅線を使う [所持: {{C|3}}]"));
            Assert.That(copperWireDetail, Is.EqualTo("HaveCount"));

            Assert.That(
                SifrahTokenDescriptionTranslator.TryTranslateDescription(
                    "gift a scrap metal [have {{C|1}}]",
                    out var giftItem,
                    out var giftItemDetail),
                Is.True);
            Assert.That(giftItem, Is.EqualTo("スクラップ金属を贈る [所持: {{C|1}}]"));
            Assert.That(giftItemDetail, Is.EqualTo("HaveCount"));
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
