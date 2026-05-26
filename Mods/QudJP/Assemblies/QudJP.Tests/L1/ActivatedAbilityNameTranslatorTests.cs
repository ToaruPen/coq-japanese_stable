using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class ActivatedAbilityNameTranslatorTests
{
    [SetUp]
    public void SetUp()
    {
        var localizationRoot = Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization");
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));
    }

    [TearDown]
    public void TearDown()
    {
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
    }

    [Test]
    public void TryTranslateVisibleName_TranslatesDeactivateTargetWithColorWrapper()
    {
        var translated = ActivatedAbilityNameTranslator.TryTranslateVisibleName(
            "Deactivate bronze long sword",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{w|青銅の長剣}}を停止"));
        });
    }

    [Test]
    public void TryTranslateVisibleName_DeactivateFallsBackWhenTargetRemainsAscii()
    {
        var translated = ActivatedAbilityNameTranslator.TryTranslateVisibleName(
            "Deactivate odd gizmo",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("Deactivate odd gizmo"));
        });
    }

    [Test]
    public void TryTranslateVisibleName_TranslatesActivateAlreadyLocalizedTarget()
    {
        var translated = ActivatedAbilityNameTranslator.TryTranslateVisibleName(
            "Activate ナインフォールドのブーツ",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("ナインフォールドのブーツを起動"));
        });
    }

    [Test]
    public void TryTranslateVisibleName_ActivateFallsBackWhenTargetRemainsAscii()
    {
        var translated = ActivatedAbilityNameTranslator.TryTranslateVisibleName(
            "Activate odd gizmo",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("Activate odd gizmo"));
        });
    }

    [Test]
    public void TryTranslateVisibleName_TranslatesLayMineTargetPrefix()
    {
        var translated = ActivatedAbilityNameTranslator.TryTranslateVisibleName(
            "Lay Mine [HE mine mk I]",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("地雷設置 [HE mine mk I]"));
        });
    }

    [TestCase("")]
    [TestCase("Deactivate ")]
    [TestCase("Deactivate {{Y|odd gizmo}}")]
    [TestCase("\u0001Deactivate bronze long sword")]
    public void TryTranslateVisibleName_DeactivateEdgeInputsPassThrough(string input)
    {
        var translated = ActivatedAbilityNameTranslator.TryTranslateVisibleName(input, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(input));
        });
    }

    [TestCase("Clone", "クローン作成")]
    [TestCase("Dig", "掘る")]
    [TestCase("Engulf", "呑み込む")]
    [TestCase("Run", "走る")]
    [TestCase("Run Over", "轢く")]
    public void TryTranslateVisibleName_TranslatesMiscProviderFixedNames(string source, string expected)
    {
        Assert.Multiple(() =>
        {
            Assert.That(ActivatedAbilityNameTranslator.TryTranslateVisibleName(source, out var translated), Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [TestCase("Belch Urchins", "ウニを吐く")]
    [TestCase("Breathe Fire", "火炎ブレス")]
    [TestCase("Breathe Ice", "氷結ブレス")]
    [TestCase("Breathe Normality Gas", "正常化ブレス")]
    [TestCase("Breathe Corrosive Gas", "腐食ブレス")]
    [TestCase("Breathe Confusion Gas", "混乱ブレス")]
    [TestCase("Breathe Stun Gas", "朦朧ブレス")]
    [TestCase("Breathe Poison Gas", "毒ブレス")]
    [TestCase("Breathe Sleep Gas", "睡眠ブレス")]
    [TestCase("Breathe Shame Gas", "恥辱ブレス")]
    [TestCase("Release Corrosive Gas", "腐食性ガス放出")]
    [TestCase("Release Sleep Gas", "睡眠ガス放出")]
    [TestCase("Release Poison Gas", "毒ガス放出")]
    [TestCase("Release Confusion Gas", "混乱ガス放出")]
    [TestCase("Release Normality Gas", "正常化ガス放出")]
    [TestCase("Release Defoliant", "落葉剤放出")]
    [TestCase("Release Fungicide", "殺真菌剤放出")]
    [TestCase("Release Glitter Dust", "グリッターダスト放出")]
    [TestCase("Release Plasma", "プラズマ放出")]
    [TestCase("Crungling Gaze", "クラングリングの視線")]
    [TestCase("Lithifying Gaze", "石化の視線")]
    [TestCase("Quill Fling", "棘毛投げ")]
    [TestCase("Temporal Fugue", "時間遁走")]
    [TestCase("Quantum Fugue", "量子フーガ")]
    [TestCase("Jump", "ジャンプ")]
    public void TryTranslateVisibleName_TranslatesActivatedAbilityAssetBridgeNames(string source, string expected)
    {
        Assert.Multiple(() =>
        {
            Assert.That(ActivatedAbilityNameTranslator.TryTranslateVisibleName(source, out var translated), Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslateVisibleName_TranslatesCloneRemainingCount()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ActivatedAbilityNameTranslator.TryTranslateVisibleName("Clone [3 left]", out var translated), Is.True);
            Assert.That(translated, Is.EqualTo("クローン作成 [残り3]"));
        });
    }

    [Test]
    public void TranslatePreservingColors_DeactivateStripsDirectTranslationMarker()
    {
        var result = ActivatedAbilityNameTranslator.TranslatePreservingColors(
            "\u0001Deactivate bronze long sword",
            nameof(ActivatedAbilityNameTranslatorTests),
            "Ability.Name");

        Assert.That(result, Is.EqualTo("Deactivate bronze long sword"));
    }

    [Test]
    public void TranslatePreservingColors_DeactivatePreservesColorWrappers()
    {
        var result = ActivatedAbilityNameTranslator.TranslatePreservingColors(
            "{{Y|Deactivate bronze long sword}}",
            nameof(ActivatedAbilityNameTranslatorTests),
            "Ability.Name");

        Assert.That(result, Is.EqualTo("{{Y|{{w|青銅の長剣}}を停止}}"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesFabricateFrameAndPreservesCaptureColor()
    {
        var translated = ActivatedAbilityNameTranslator.TranslatePreservingColors(
            "&CFabricate {{Y|lead slug}}",
            nameof(ActivatedAbilityNameTranslatorTests),
            nameof(ActivatedAbilityNameTranslatorTests));

        Assert.That(translated, Is.EqualTo("&C{{Y|lead slug}}を生成"));
    }
}
