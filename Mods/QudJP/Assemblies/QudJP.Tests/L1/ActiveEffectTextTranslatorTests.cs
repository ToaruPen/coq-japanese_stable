using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class ActiveEffectTextTranslatorTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-active-effect-text-l1", Guid.NewGuid().ToString("N"));
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
    public void TryTranslateText_DoesNotDuplicateNestedSameColorWrapper_WhenTranslatedExactOwnsMarkup()
    {
        WriteDictionary(("wet", "{{B|濡れた}}"));

        var changed = ActiveEffectTextTranslator.TryTranslateText(
            "{{B|{{B|wet}}}}",
            "ActiveEffectTextTranslatorTests",
            "ActiveEffects.Description.LiquidCovered",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(translated, Is.EqualTo("{{B|濡れた}}"));
            Assert.That(translated, Does.Not.Contain("{{B|{{B|"));
            Assert.That(translated, Does.Not.Contain("{{B}}"));
        });
    }

    [Test]
    public void TryTranslateText_ComposesCoveredLiquidFromAdjectiveAndLiquid_WhenCoveredLiquidIsColoredByParts()
    {
        WriteDictionary(("salty water", "塩水"));
        WriteScopedDictionary(
            "ui-liquid-adjectives.ja.json",
            ("salty", "XRL.Liquids.Adjective", "塩気のある"));
        WriteScopedDictionary(
            "ui-liquids.ja.json",
            ("water", "XRL.Liquids", "水"));

        var changed = ActiveEffectTextTranslator.TryTranslateText(
            "Covered in 43 dram of {{Y|salty}} {{B|water}}.",
            "ActiveEffectTextTranslatorTests",
            "ActiveEffects.Details.LiquidCovered",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(translated, Is.EqualTo("塩気のある水を43ドラム浴びている。"));
            Assert.That(translated, Does.Not.Contain("{{Y|}}"));
            Assert.That(translated, Does.Not.Contain("{{B|}}"));
        });
    }

    [Test]
    public void TryTranslateText_TranslatesCoveredLiquidWithMultiWordDominantLiquid()
    {
        WriteScopedDictionary(
            "ui-liquid-adjectives.ja.json",
            ("dilute", "XRL.Liquids.Adjective", "薄めの"),
            ("bloody", "XRL.Liquids.Adjective", "血混じりの"));
        WriteScopedDictionary(
            "ui-liquids.ja.json",
            ("warm static", "XRL.Liquids", "ウォームスタティック"));

        var changed = ActiveEffectTextTranslator.TryTranslateText(
            "Covered in 12 drams of dilute bloody warm static.",
            "ActiveEffectTextTranslatorTests",
            "ActiveEffects.Details.LiquidCovered",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(translated, Is.EqualTo("薄めの血混じりのウォームスタティックを12ドラム浴びている。"));
        });
    }

    [Test]
    public void TryTranslateText_TranslatesRuntimeObservedMultiAdjectiveLiquidDetails()
    {
        WriteScopedDictionary(
            "ui-liquid-adjectives.ja.json",
            ("brackish", "XRL.Liquids.Adjective", "塩分混じりの"),
            ("bloody", "XRL.Liquids.Adjective", "血混じりの"));
        WriteScopedDictionary(
            "ui-liquids.ja.json",
            ("slime", "XRL.Liquids", "粘液"));

        var changed = ActiveEffectTextTranslator.TryTranslateText(
            "Covered in 31 dram of {{w|brackish}} {{r|bloody}} {{slimy|slime}}.",
            "ActiveEffectTextTranslatorTests",
            "ActiveEffects.Details.LiquidCovered",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(translated, Is.EqualTo("塩分混じりの血混じりの粘液を31ドラム浴びている。"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("brackish bloody slime"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TryTranslateText_DoesNotRecordMissingKey_ForGeneratedMoveSpeedLine()
    {
        var changed = ActiveEffectTextTranslator.TryTranslateText(
            "-20 move speed.",
            "ActiveEffectTextTranslatorTests",
            "ActiveEffects.Details.Wading",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(translated, Is.EqualTo("移動速度 -20。"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("-20 move speed."), Is.EqualTo(0));
        });
    }

    [Test]
    public void TryTranslateText_TranslatesActiveEffectsWindowMoveSpeedLineWithoutPeriod()
    {
        var changed = ActiveEffectTextTranslator.TryTranslateText(
            "-10 Move Speed",
            "ActiveEffectTextTranslatorTests",
            "ActiveEffects.Details.Interdicted",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(translated, Is.EqualTo("移動速度 -10。"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("-10 Move Speed"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TryTranslateText_TranslatesGeneratedAllMentalAttributesLine()
    {
        var changed = ActiveEffectTextTranslator.TryTranslateText(
            "-6 to all mental attributes",
            "ActiveEffectTextTranslatorTests",
            "ActiveEffects.Details.Confused",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(translated, Is.EqualTo("全精神属性に -6"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("-6 to all mental attributes"), Is.EqualTo(0));
        });
    }

    [TestCase("dominated (3 turns remaining)", "支配された（残り3ターン）")]
    [TestCase("time-dilated ({{C|-40}} Quickness)", "時間遅延 ({{C|-40}} クイックネス)")]
    [TestCase("{{C|lying on a chair}}", "{{C|椅子に横たわっている}}")]
    [TestCase("{{C|lying on a 寝袋}}", "{{C|寝袋に横たわっている}}")]
    [TestCase("{{B|engulfed by a starapple tree}}", "{{B|スターアップルの木に呑み込まれている}}")]
    [TestCase("{{G|enclosed in a glass bottle}}", "{{G|ガラス瓶に閉じ込められている}}")]
    [TestCase("{{y|sitting on a stool}}", "{{y|腰掛けに座っている}}")]
    [TestCase("{{y|sitting on a フロアクッション}}", "{{y|フロアクッションに座っている}}")]
    [TestCase("{{C|piloting a hovercraft}}", "{{C|ホバークラフトを操縦中}}")]
    [TestCase("{{R|marked by a snapjaw hunter}}", "{{R|スナップジョーの狩人にマークされている}}")]
    [TestCase("{{r|cleaved ({{C|-3 AV}})}}", "{{r|裂かれた（{{C|-3 AV}}）}}")]
    [TestCase("{{psionic|psionically cleaved (-2 MA)}}", "{{psionic|精神的に裂かれた（-2 MA）}}")]
    public void TryTranslateText_TranslatesGeneratedDescriptionFamilies(string source, string expected)
    {
        WriteDictionary(
            ("a chair", "椅子"),
            ("a starapple tree", "スターアップルの木"),
            ("a glass bottle", "ガラス瓶"),
            ("a stool", "腰掛け"),
            ("a hovercraft", "ホバークラフト"),
            ("a snapjaw hunter", "スナップジョーの狩人"));
        WriteScopedDictionary(
            "Scoped/world-effects-generated-templates.ja.json",
            ("dominated ({0} turns remaining)", "XRL.World.Effects.Dominated.GetDescription", "支配された（残り{0}ターン）"),
            ("time-dilated ({{C|-{0}}} Quickness)", "XRL.World.Effects.ITimeDilated.GetDescription", "時間遅延 ({{C|-{0}}} クイックネス)"),
            ("lying on {0}", "XRL.World.Effects.Prone.GetDescription", "{0}に横たわっている"),
            ("engulfed by {0}", "XRL.World.Effects.Engulfed.DisplayName", "{0}に呑み込まれている"),
            ("enclosed in {0}", "XRL.World.Effects.Enclosed.DisplayName", "{0}に閉じ込められている"),
            ("sitting on {0}", "XRL.World.Effects.Sitting.DisplayName", "{0}に座っている"),
            ("piloting {0}", "XRL.World.Effects.Piloting.DisplayName", "{0}を操縦中"),
            ("marked by {0}", "XRL.World.Effects.RifleMark.GetDescription", "{0}にマークされている"),
            ("cleaved ({{C|-{0} AV}})", "XRL.World.Effects.ShatterArmor.GetDescription", "裂かれた（{{C|-{0} AV}}）"),
            ("psionically cleaved (-{0} MA)", "XRL.World.Effects.ShatterMentalArmor.GetDescription", "精神的に裂かれた（-{0} MA）"));

        var changed = ActiveEffectTextTranslator.TryTranslateText(
            source,
            "ActiveEffectTextTranslatorTests",
            "ActiveEffects.Description.Generated",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [TestCase(
        "Acts semi-randomly.\n-6 DV\n-6 MA",
        "半ばランダムに行動する。\n-6 DV\n-6 MA")]
    [TestCase(
        "Acts semi-randomly.\n-6 DV\n-6 MA\n-4 to all mental attributes",
        "半ばランダムに行動する。\n-6 DV\n-6 MA\n全精神属性に -4")]
    [TestCase(
        "Acts semi-randomly.\n  -6 DV\n  -6 MA\n  -4 to all mental attributes",
        "半ばランダムに行動する。\n-6 DV\n-6 MA\n全精神属性に -4")]
    public void TryTranslateText_TranslatesGeneratedConfusionDetailsBeforeLineFallback(string source, string expected)
    {
        WriteScopedDictionary(
            "Scoped/world-effects-generated-templates.ja.json",
            ("Acts semi-randomly.\n-{0} DV\n-{0} MA", "XRL.World.Effects.Confused.GetDetails", "半ばランダムに行動する。\n-{0} DV\n-{0} MA"),
            ("Acts semi-randomly.\n-{0} DV\n-{0} MA\n-{1} to all mental attributes", "XRL.World.Effects.Confused.GetDetails", "半ばランダムに行動する。\n-{0} DV\n-{0} MA\n全精神属性に -{1}"));

        var changed = ActiveEffectTextTranslator.TryTranslateText(
            source,
            "ActiveEffectTextTranslatorTests",
            "ActiveEffects.Details.Confused",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Acts semi-randomly."), Is.EqualTo(0));
            Assert.That(Translator.GetMissingKeyHitCountForTests("-6 DV"), Is.EqualTo(0));
        });
    }

    [TestCase(
        "-6 Agility.\n-5 DV.\n-80 move speed.\nMust spend a turn to stand up.",
        "敏捷-6。\nDV-5。\n移動速度 -80。\n立ち上がるには1ターンを費やす必要がある。")]
    [TestCase(
        "Slightly improves natural healing rate.\nAids in examining and disassembling artifacts.\n-6 DV.\nMust spend a turn to stand up before moving.",
        "自然治癒速度がわずかに向上する。\n遺物の調査と分解に役立つ。\nDV-6。\n移動する前に立ち上がるには1ターンを費やす必要がある。")]
    public void TryTranslateText_TranslatesRuntimeBodyPositionDetails(string source, string expected)
    {
        var changed = ActiveEffectTextTranslator.TryTranslateText(
            source,
            "ActiveEffectTextTranslatorTests",
            "ActiveEffects.Details.BodyPosition",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [TestCase(
        "Unknown body position detail.",
        false,
        "Unknown body position detail.")]
    [TestCase("", false, "")]
    [TestCase("\u0001-6 Agility.", false, "\u0001-6 Agility.")]
    [TestCase(
        "<color=#ff0>-6 Agility.\n-5 DV.\n-80 move speed.\nMust spend a turn to stand up.</color>",
        true,
        "<color=#ff0>敏捷-6。\nDV-5。\n移動速度 -80。\n立ち上がるには1ターンを費やす必要がある。</color>")]
    public void TryTranslateText_BodyPositionDetails_CoversFallbackAndEdges(
        string source,
        bool expectedChanged,
        string expected)
    {
        var changed = ActiveEffectTextTranslator.TryTranslateText(
            source,
            "ActiveEffectTextTranslatorTests",
            "ActiveEffects.Details.BodyPosition",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.EqualTo(expectedChanged));
            Assert.That(translated, Is.EqualTo(expected));
        });
    }

    [TestCase(
        "+2 DV while wielding a long blade in the primary hand.",
        "主手に長剣を装備しているあいだDV+2。")]
    [TestCase(
        "+3 DV while wielding a long blade in the primary hand.",
        "主手に長剣を装備しているあいだDV+3。")]
    [TestCase(
        "+1 to your penetration roll and -2 to hit while wielding a long blade in the primary hand.",
        "主手に長剣を装備しているあいだ貫通判定+1、命中-2。")]
    [TestCase(
        "+2 to your penetration roll and -3 to hit while wielding a long blade in the primary hand.",
        "主手に長剣を装備しているあいだ貫通判定+2、命中-3。")]
    [TestCase(
        "+2 to hit while wielding a long blade in the primary hand.",
        "主手に長剣を装備しているあいだ命中+2。")]
    [TestCase(
        "+3 to hit while wielding a long blade in the primary hand.",
        "主手に長剣を装備しているあいだ命中+3。")]
    public void TryTranslateText_TranslatesLongBladeStanceGeneratedDetails(string source, string expected)
    {
        WriteLongbladeStanceTemplateDictionary();

        var changed = ActiveEffectTextTranslator.TryTranslateText(
            source,
            "ActiveEffectTextTranslatorTests",
            "ActiveEffects.Details.LongbladeStance",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(translated, Is.EqualTo(expected));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test]
    public void TryTranslateText_LongBladeStanceGeneratedDetails_FallsBackToEnglishWhenTemplateDictionaryMisses()
    {
        var source = "+4 DV while wielding a long blade in the primary hand.";

        var changed = ActiveEffectTextTranslator.TryTranslateText(
            source,
            "ActiveEffectTextTranslatorTests",
            "ActiveEffects.Details.LongbladeStance",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(
                Translator.GetMissingKeyHitCountForTests("+{0} DV while wielding a long blade in the primary hand."),
                Is.EqualTo(1));
        });
    }

    [Test]
    public void TryTranslateText_LongBladeStanceGeneratedDetails_PreservesWholeColorTag()
    {
        WriteLongbladeStanceTemplateDictionary();
        const string source = "<color=#FF0000>+2 DV while wielding a long blade in the primary hand.</color>";

        var changed = ActiveEffectTextTranslator.TryTranslateText(
            source,
            "ActiveEffectTextTranslatorTests",
            "ActiveEffects.Details.LongbladeStance",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(translated, Is.EqualTo("<color=#FF0000>主手に長剣を装備しているあいだDV+2。</color>"));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test]
    public void TryTranslateText_LongBladeStanceGeneratedDetails_DirectMarkerFallsThroughSafely()
    {
        WriteLongbladeStanceTemplateDictionary();
        const string source = "\u0001+2 DV while wielding a long blade in the primary hand.";

        var changed = ActiveEffectTextTranslator.TryTranslateText(
            source,
            "ActiveEffectTextTranslatorTests",
            "ActiveEffects.Details.LongbladeStance",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(1));
        });
    }

    [Test]
    public void TryTranslateText_LongBladeStanceGeneratedDetails_EmptyInputFallsThroughSafely()
    {
        WriteLongbladeStanceTemplateDictionary();

        var changed = ActiveEffectTextTranslator.TryTranslateText(
            string.Empty,
            "ActiveEffectTextTranslatorTests",
            "ActiveEffects.Details.LongbladeStance",
            out var translated);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.False);
            Assert.That(translated, Is.EqualTo(string.Empty));
            Assert.That(Translator.GetMissingKeyHitCountForTests(string.Empty), Is.EqualTo(0));
        });
    }

    private void WriteLongbladeStanceTemplateDictionary()
    {
        WriteScopedDictionary(
            "Scoped/world-effects-generated-templates.ja.json",
            ("+{0} DV while wielding a long blade in the primary hand.", "XRL.World.Effects.LongbladeStance_Defensive.GetDetails", "主手に長剣を装備しているあいだDV+{0}。"),
            ("+{0} to your penetration roll and -{1} to hit while wielding a long blade in the primary hand.", "XRL.World.Effects.LongbladeStance_Aggressive.GetDetails", "主手に長剣を装備しているあいだ貫通判定+{0}、命中-{1}。"),
            ("+{0} to hit while wielding a long blade in the primary hand.", "XRL.World.Effects.LongbladeStance_Dueling.GetDetails", "主手に長剣を装備しているあいだ命中+{0}。"));
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        WriteDictionaryFile("active-effect-text.ja.json", builder.ToString());
    }

    private void WriteScopedDictionary(string fileName, params (string key, string context, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].key));
            builder.Append("\",\"context\":\"");
            builder.Append(EscapeJson(entries[index].context));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        WriteDictionaryFile(fileName, builder.ToString());
    }

    private void WriteDictionaryFile(string fileName, string contents)
    {
        var path = Path.Combine(tempDirectory, fileName);
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        File.WriteAllText(path, contents, Utf8WithoutBom);
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }
}
