using System.Text;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class WorldModsTextTranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-world-mods-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        DynamicTextObservability.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        DynamicTextObservability.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TryTranslate_UsesScopedExactLookup()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Airfoil: This item can be thrown at +4 throwing range.", "エアフォイル: この品は投擲射程が+4される。"));

        var ok = WorldModsTextTranslator.TryTranslate(
            "Airfoil: This item can be thrown at +4 throwing range.",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("エアフォイル: この品は投擲射程が+4される。"));
        });
    }

    [Test]
    public void TryTranslate_PreservesColorsForScopedExactLookup()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Scoped: This weapon has increased accuracy.", "スコープ付き: この武器は命中精度が向上する。"));

        var ok = WorldModsTextTranslator.TryTranslate(
            "{{Y|Scoped: This weapon has increased accuracy.}}",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("{{Y|スコープ付き: この武器は命中精度が向上する。}}"));
        });
    }

    [Test]
    public void TryTranslate_TranslatesImprovedMutationTemplate()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Grants you {0} at level {1}. If you already have {0}, its level is increased by {1}.", "{0}をレベル{1}で得る。すでに{0}を持っている場合、そのレベルが{1}上昇する。"),
            ("Temporal Fugue", "時間遁走"));

        var ok = WorldModsTextTranslator.TryTranslate(
            "Grants you Temporal Fugue at level 3. If you already have Temporal Fugue, its level is increased by 3.",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("時間遁走をレベル3で得る。すでに時間遁走を持っている場合、そのレベルが3上昇する。"));
        });
    }

    [TestCase(
        "Anti-gravity: When powered, this item's weight is reduced by 20% plus 2 lbs.",
        "反重力: 通電中、この品の重量は20%減り、さらに2lbs軽くなる。")]
    [TestCase(
        "Co-processor: When powered, this item grants +2 Intelligence and provides 13 units of compute power to the local lattice.",
        "共同処理装置: 通電中、知力に+2を与え、局所格子に13ユニットの演算力を供給する。")]
    [TestCase(
        "Co-Processor: When powered, this item grants bonus Intelligence and provides compute power to the local lattice.",
        "共同処理装置: 通電中、知力にボーナスを与え、局所格子に演算力を供給する。")]
    [TestCase(
        "Counterweighted: Adds +2 to hit.",
        "つり合い調整: 命中に+2のボーナスを与える。")]
    [TestCase(
        "Counterweighted: Adds a bonus to hit.",
        "つり合い調整: 命中にボーナスを与える。")]
    [TestCase(
        "Displacer: When powered, this weapon randomly teleports its target 1-6 tiles away on a successful hit.",
        "位相転移: 通電中、この武器は命中時に対象を無作為に1-6マス離れた場所へ転移させる。")]
    [TestCase(
        "Electrified: When powered, this weapon deals an additional 2-3 electrical damage on hit.",
        "電化: 通電中、この武器は命中時に追加で2-3の電撃ダメージを与える。")]
    [TestCase(
        "Flaming: When powered, this weapon deals additional heat damage on hit.",
        "火炎: 通電中、この武器は命中時に追加の熱ダメージを与える。")]
    [TestCase(
        "Freezing: When powered, this weapon deals additional cold damage on hit.",
        "冷却: 通電中、この武器は命中時に追加の冷気ダメージを与える。")]
    [TestCase(
        "Feathered: This item grants the wearer +250 reputation with birds.",
        "羽飾り: 装着者に鳥類との評判+250を与える。")]
    [TestCase(
        "Scaled: This item grants the wearer +250 reputation with unshelled reptiles.",
        "鱗状の: 装着者に甲無し爬虫類との評判+250を与える。")]
    [TestCase(
        "Snail-Encrusted: This item is crawling with tiny snails and grants the wearer +250 reputation with mollusks.",
        "巻貝まみれ: 小さなカタツムリが這っており、装着者に軟体動物との評判+250を与える。")]
    [TestCase(
        "Offhand Attack Chance: 15%",
        "オフハンド命中率: 15%")]
    public void TryTranslate_TranslatesDynamicWorldModsTemplates(string source, string expected)
    {
        WriteDynamicWorldModsDictionary();

        var ok = WorldModsTextTranslator.TryTranslate(
            source,
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_TranslatesNestedDataDiskItemModificationTemplate()
    {
        WriteDynamicWorldModsDictionary();

        var ok = WorldModsTextTranslator.TryTranslate(
            "Adds item modification: Counterweighted: Adds +2 to hit.",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("アイテム改造: つり合い調整: 命中に+2のボーナスを与える。"));
        });
    }

    [Test]
    public void TryTranslate_PreservesColorsForDynamicWorldModsTemplate()
    {
        WriteDynamicWorldModsDictionary();

        var ok = WorldModsTextTranslator.TryTranslate(
            "{{Y|Electrified: When powered, this weapon deals an additional 2-3 electrical damage on hit.}}",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("{{Y|電化: 通電中、この武器は命中時に追加で2-3の電撃ダメージを与える。}}"));
        });
    }

    [Test]
    public void TryTranslate_FallbackToEnglishForUntranslatedKey()
    {
        WriteDictionary("world-mods.ja.json");

        var ok = WorldModsTextTranslator.TryTranslate(
            "This is an untranslated English phrase.",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo("This is an untranslated English phrase."));
        });
    }

    [Test]
    public void TryTranslate_HandlesEmptyInput()
    {
        WriteDictionary("world-mods.ja.json");

        var ok = WorldModsTextTranslator.TryTranslate(
            "",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(translated, Is.EqualTo(""));
        });
    }

    [Test]
    public void TryTranslate_PreservesMarkerAndColorTagsCombined()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Flaming: When powered, this weapon deals additional heat damage on hit.", "火炎: 通電中、この武器は命中時に追加の熱ダメージを与える。"));

        var ok = WorldModsTextTranslator.TryTranslate(
            "\x01{{r|Flaming: When powered, this weapon deals additional heat damage on hit.}}",
            "DescriptionShortDescriptionPatch",
            "Description.WorldMods",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("\x01{{r|火炎: 通電中、この武器は命中時に追加の熱ダメージを与える。}}"));
        });
    }

    [Test]
    public void TryTranslateCompareStatusLine_TranslatesBowsAndRifles()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Weapon Class:", "武器カテゴリ:"),
            ("Bows && Rifles", "弓・ライフル"));

        var ok = StatusLineTranslationHelpers.TryTranslateCompareStatusLine(
            "Weapon Class: Bows && Rifles",
            "DescriptionShortDescriptionPatch",
            "Description.CompareStatus",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("武器カテゴリ: 弓・ライフル"));
        });
    }

    [Test]
    public void TryTranslateCompareStatusLine_TranslatesScopedWeaponClassValue()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Weapon Class:", "武器カテゴリ:"),
            ("Cudgel (dazes on critical hit)", "棍棒（クリティカル時に朦朧付与）"));

        var ok = StatusLineTranslationHelpers.TryTranslateCompareStatusLine(
            "Weapon Class: Cudgel (dazes on critical hit)",
            "DescriptionShortDescriptionPatch",
            "Description.CompareStatus",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("武器カテゴリ: 棍棒（クリティカル時に朦朧付与）"));
        });
    }

    [Test]
    public void TryTranslateCompareStatusLine_TranslatesRequiresAndWeightPrefixes()
    {
        WriteDictionary(
            "ui-default.ja.json",
            ("Requires:", "要件："),
            ("Weight:", "重量："),
            ("Tinker I", "ティンカーI"));

        var requiresOk = StatusLineTranslationHelpers.TryTranslateCompareStatusLine(
            "Requires: Tinker I",
            "DescriptionShortDescriptionPatch",
            "Description.CompareStatus",
            out var requiresTranslated);
        var weightOk = StatusLineTranslationHelpers.TryTranslateCompareStatusLine(
            "Weight: 1 lbs.",
            "DescriptionShortDescriptionPatch",
            "Description.CompareStatus",
            out var weightTranslated);

        Assert.Multiple(() =>
        {
            Assert.That(requiresOk, Is.True);
            Assert.That(requiresTranslated, Is.EqualTo("要件： ティンカーI"));
            Assert.That(weightOk, Is.True);
            Assert.That(weightTranslated, Is.EqualTo("重量： 1 lbs."));
        });
    }

    [Test]
    public void TryTranslateActiveEffectsLine_PartiallyTranslatesKnownEffectsAndKeepsMissingEffectsVisible()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("ACTIVE EFFECTS:", "発動中の効果:"),
            ("wet", "濡れている"));

        var ok = StatusLineTranslationHelpers.TryTranslateActiveEffectsLine(
            "ACTIVE EFFECTS: unknown, wet",
            "TestRoute",
            "Description.ActiveEffects",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(translated, Is.EqualTo("発動中の効果: unknown、濡れている"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("unknown"), Is.EqualTo(1));
            Assert.That(
                DynamicTextObservability.GetRouteFamilyHitCountForTests("TestRoute", "Description.ActiveEffects"),
                Is.GreaterThan(0));
        });
    }

    private void WriteDynamicWorldModsDictionary()
    {
        WriteDictionary(
            "world-mods.ja.json",
            ("Adds item modification: {0}", "アイテム改造: {0}"),
            ("Anti-gravity: When powered, this item's weight is reduced by {0}% plus {1} {2}.", "反重力: 通電中、この品の重量は{0}%減り、さらに{1}{2}軽くなる。"),
            ("Co-Processor: When powered, this item grants {0} {1} and provides compute power to the local lattice.", "共同処理装置: 通電中、{1}に{0}を与え、局所格子に演算力を供給する。"),
            ("Co-Processor: When powered, this item grants {0} {1} and provides {2} units of compute power to the local lattice.", "共同処理装置: 通電中、{1}に{0}を与え、局所格子に{2}ユニットの演算力を供給する。"),
            ("Counterweighted: Adds a bonus to hit.", "つり合い調整: 命中にボーナスを与える。"),
            ("Counterweighted: Adds {0} to hit.", "つり合い調整: 命中に{0}のボーナスを与える。"),
            ("Displacer: When powered, this weapon randomly teleports its target {0} tiles away on a successful hit.", "位相転移: 通電中、この武器は命中時に対象を無作為に{0}マス離れた場所へ転移させる。"),
            ("Electrified: When powered, this weapon deals additional electrical damage on hit.", "電化: 通電中、この武器は命中時に追加の電撃ダメージを与える。"),
            ("Electrified: When powered, this weapon deals an additional {0} electrical damage on hit.", "電化: 通電中、この武器は命中時に追加で{0}の電撃ダメージを与える。"),
            ("Flaming: When powered, this weapon deals additional heat damage on hit.", "火炎: 通電中、この武器は命中時に追加の熱ダメージを与える。"),
            ("Flaming: When powered, this weapon deals an additional {0} heat damage on hit.", "火炎: 通電中、この武器は命中時に追加で{0}の熱ダメージを与える。"),
            ("Freezing: When powered, this weapon deals additional cold damage on hit.", "冷却: 通電中、この武器は命中時に追加の冷気ダメージを与える。"),
            ("Freezing: When powered, this weapon deals an additional {0} cold damage on hit.", "冷却: 通電中、この武器は命中時に追加で{0}の冷気ダメージを与える。"),
            ("Feathered: This item grants the wearer {0} reputation with birds.", "羽飾り: 装着者に鳥類との評判{0}を与える。"),
            ("Offhand Attack Chance: {0}%", "オフハンド命中率: {0}%"),
            ("Scaled: This item grants the wearer {0} reputation with unshelled reptiles.", "鱗状の: 装着者に甲無し爬虫類との評判{0}を与える。"),
            ("Snail-Encrusted: This item is crawling with tiny snails and grants the wearer {0} reputation with mollusks.", "巻貝まみれ: 小さなカタツムリが這っており、装着者に軟体動物との評判{0}を与える。"),
            ("Intelligence", "知力"));
    }

    private void WriteDictionary(string fileName, params (string key, string text)[] entries)
    {
        using var writer = new StreamWriter(Path.Combine(tempDirectory, fileName), append: false, Utf8WithoutBom);
        writer.Write("{\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                writer.Write(',');
            }

            writer.Write("{\"key\":\"");
            writer.Write(EscapeJson(entries[index].key));
            writer.Write("\",\"text\":\"");
            writer.Write(EscapeJson(entries[index].text));
            writer.Write("\"}");
        }

        writer.WriteLine("]}");
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
