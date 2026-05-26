using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class UITextSkinTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-uitextskin-l2", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        ScopedDictionaryLookup.ResetForTests();
        SinkObservation.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        SinkObservation.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Prefix_ReturnsSourceUnchanged_ObservationOnly()
    {
        WriteDictionary(("Hello", "こんにちは"));

        var text = "Hello";
        UITextSkinTranslationPatch.Prefix(ref text);

        Assert.That(text, Is.EqualTo("Hello"));
    }

    [Test]
    public void Prefix_PassesThroughUnknownText()
    {
        WriteDictionary(("Hello", "こんにちは"));

        var text = "Unknown text";
        UITextSkinTranslationPatch.Prefix(ref text);

        Assert.That(text, Is.EqualTo("Unknown text"));
    }

    [Test]
    public void Prefix_ReturnsColorWrappedSourceUnchanged_ObservationOnly()
    {
        WriteDictionary(("Hello", "こんにちは"));

        var text = "{{W|Hello}}";
        UITextSkinTranslationPatch.Prefix(ref text);

        Assert.That(text, Is.EqualTo("{{W|Hello}}"));
    }

    [TestCase("{{W|発動中の効果}}{{Y| - クラミルの蒸留所}}")]
    [TestCase("&W発動中の効果&Y - クラミルの蒸留所")]
    public void ShouldRepairActiveEffectsTitle_DetectsColoredTitleText(string text)
    {
        Assert.That(UITextSkinTranslationPatch.ShouldRepairActiveEffectsTitleForTests(text), Is.True);
    }

    [TestCase("発動中の効果はない。")]
    [TestCase("{{W|[Esc]}} 閉じる")]
    public void ShouldRepairActiveEffectsTitle_IgnoresNonTitleText(string text)
    {
        Assert.That(UITextSkinTranslationPatch.ShouldRepairActiveEffectsTitleForTests(text), Is.False);
    }

    [Test]
    public void BuildActiveEffectsTitleRtf_ConvertsLocalizedTitleToTmpRichText()
    {
        var result = UITextSkinTranslationPatch.BuildActiveEffectsTitleRtfForTests(
            "{{W|発動中の効果}}{{Y| - ウォーターヴァイン農家}}");

        Assert.That(
            result,
            Is.EqualTo("<color=#CFC041FF>発動中の効果</color><color=#40A4B9FF> - ウォーターヴァイン農家</color>"));
    }

    [Test]
    public void BuildActiveEffectsTitleRtf_EscapesTmpMarkupInTargetName()
    {
        var result = UITextSkinTranslationPatch.BuildActiveEffectsTitleRtfForTests(
            "{{W|発動中の効果}}{{Y| - <snapjaw> friend}}");

        Assert.That(
            result,
            Is.EqualTo("<color=#CFC041FF>発動中の効果</color><color=#40A4B9FF> - &lt;snapjaw&gt; friend</color>"));
    }

    [Test]
    public void Prefix_StripsDirectTranslationMarkerBeforeSinkTranslation()
    {
        var text = "\u0001{{W|熊は防いだ。}}";

        var output = TestTraceHelper.CaptureTrace(() => UITextSkinTranslationPatch.Prefix(ref text));

        Assert.That(text, Is.EqualTo("{{W|熊は防いだ。}}"));
        Assert.That(output, Does.Contain("translation_status='direct_marker'"));
        Assert.That(output, Does.Contain("direct_marker_status='present'"));
    }

    [Test]
    public void Prefix_StripsEmbeddedDirectTranslationMarkerBeforeFinalDisplay()
    {
        var text = "advertisement for \u0001{{M|クユラミルの蒸留所, 伝説の樹液商}}";

        UITextSkinTranslationPatch.Prefix(ref text);

        Assert.That(text, Is.EqualTo("advertisement for {{M|クユラミルの蒸留所, 伝説の樹液商}}"));
    }

    [TestCaseSource(typeof(QudJP.Tests.L1.ColorRouteInvariantCases), nameof(QudJP.Tests.L1.ColorRouteInvariantCases.UiTextSkinCases))]
    public void TranslatePreservingColors_PreservesSharedInvariantCases(QudJP.Tests.L1.ColorTranslationCase testCase)
    {
        WriteDictionary(testCase.Entries.ToArray());

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(testCase.Source, testCase.Context);

        Assert.That(translated, Is.EqualTo(testCase.Expected));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesDisplayNameWithClauseAtUiTextSink()
    {
        WriteContextDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("beamsplitter", "GetDisplayName.Adjective", "{{R-R-r-r-g-g-G-G-B-B-b-b sequence|ビームスプリッタ装着}}"),
            ("beamsplitter", null, "{{R-R-r-r-g-g-G-G-B-B-b-b sequence|ビームスプリッタ装着}}"));

        var source =
            "アイゲンライフル with {{R-R-r-r-g-g-G-G-B-B-b-b sequence|beamsplitter}} {{W|\u001a}}10 {{r|\u0003}}1d12 {{y|[{{w|フィジェット}} {{c|セル}} {{b|\u0004}}0 {{K|\t}}0 {{y|({{g|残量多}})}}]}}";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(UITextSkinTranslationPatch));

        Assert.That(
            translated,
            Is.EqualTo("アイゲンライフル（{{R-R-r-r-g-g-G-G-B-B-b-b sequence|ビームスプリッタ装着}}） {{W|\u001a}}10 {{r|\u0003}}1d12 {{y|[{{w|フィジェット}} {{c|セル}} {{b|\u0004}}0 {{K|\t}}0 {{y|({{g|残量多}})}}]}}"));
    }

    [TestCase("[Esc]")]
    [TestCase("[Space]")]
    [TestCase("[]")]
    [TestCase("[Esc] Cancel")]
    [TestCase("SP: 99")]
    [TestCase("1.0.4\nbuild 2.0.210.24")]
    [TestCase("quit")]
    public void Prefix_SkipsKnownObservabilityNoiseTokens(string text)
    {
        WriteDictionary(("quit", "終了"), ("[Esc]", "[Esc-JP]"));

        var original = text;
        var output = TestTraceHelper.CaptureTrace(() => UITextSkinTranslationPatch.Prefix(ref text));

        Assert.Multiple(() =>
        {
            Assert.That(text, Is.EqualTo(original));
            Assert.That(Translator.GetMissingKeyHitCountForTests(original), Is.EqualTo(0));
            Assert.That(output, Does.Contain("translation_status='skipped'"));
        });
    }

    [TestCase("クラシック")]
    [TestCase("チュートリアル\n[A]")]
    [TestCase("：ゲームモードを選択：")]
    [TestCase(" >{{K| . . . . . . . ■ .  . . . . . . . ■")]
    [TestCase("   ")]
    public void TranslatePreservingColors_SkipsAlreadyLocalizedUITextSinkText(string text)
    {
        WriteDictionary(("クラシック", "CLASSIC-JP"));

        var output = TestTraceHelper.CaptureTrace(() =>
            Assert.That(
                UITextSkinTranslationPatch.TranslatePreservingColors(text, nameof(UITextSkinTranslationPatch)),
                Is.EqualTo(text)));

        Assert.Multiple(() =>
        {
            Assert.That(Translator.GetMissingKeyHitCountForTests(text), Is.EqualTo(0));
            Assert.That(output, Does.Contain("translation_status='skipped'"));
        });
    }

    [TestCase("{{Y|}}{{Y| . . .}}>{{K|. . . . . ■ .  . . . . . . . ■  . . . . . . . . ■  . . . . . . . . ■  . . . . . . . . {{Y|}}")]
    [TestCase("{{W|{{W|[k]}} {{y|攻撃}}}}")]
    [TestCase("{{y|{{y|安らぎあれ、friend。\\n\\nLive and drink.}}\\n\\n}}")]
    [TestCase("{{y|{{y|汎用の会話文。}}\\n\\n}}")]
    public void TranslatePreservingColors_DoesNotReportSemanticDrift_ForSemanticallyIdempotentQudMarkup(string text)
    {
        var output = TestTraceHelper.CaptureTrace(() =>
            Assert.That(
                UITextSkinTranslationPatch.TranslatePreservingColors(text, nameof(UITextSkinTranslationPatch)),
                Is.EqualTo(text)));

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Contain("translation_status='skipped'"));
            Assert.That(output, Does.Contain("markup_semantic_status=clean"));
            Assert.That(output, Does.Not.Contain("markup_semantic_status=drift"));
        });
    }

    [Test]
    public void TranslatePreservingColors_RecordsAlreadyLocalizedDirectRouteText()
    {
        var text = "新しいゲーム";

        var output = TestTraceHelper.CaptureTrace(() =>
            Assert.That(
                UITextSkinTranslationPatch.TranslatePreservingColors(text, nameof(MainMenuLocalizationPatch)),
                Is.EqualTo(text)));

        Assert.That(output, Does.Contain("translation_status='already_localized'"));
    }

    [Test]
    public void TranslatePreservingColors_DoesNotSuppressJapaneseTextForNonSinkContexts()
    {
        var text = "クラシック";

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(text, nameof(CharGenLocalizationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(text));
            Assert.That(Translator.GetMissingKeyHitCountForTests(text), Is.EqualTo(0));
        });
    }

    [TestCase("新しいゲーム", nameof(MainMenuLocalizationPatch))]
    [TestCase("：プレイ方式を選択：", nameof(CharGenLocalizationPatch))]
    [TestCase("チュートリアル\n[A]", nameof(CharGenLocalizationPatch))]
    [TestCase("Caves of Qud の基礎を学ぶ。", nameof(CharGenLocalizationPatch))]
    [TestCase("有機生命体の史料庫と照合する微小なグラフェンアレイ。\n\n生物クリーチャーの正確なHP・AV・DVを参照できる。", nameof(CharGenLocalizationPatch))]
    [TestCase("甲殻", nameof(GetDisplayNamePatch))]
    [TestCase("木製バックラー", nameof(GetDisplayNameProcessPatch))]
    [TestCase("イッサカリ族", nameof(FactionsStatusScreenTranslationPatch))]
    [TestCase("ヴァインウェハー", nameof(InventoryLocalizationPatch))]
    [TestCase("ウォーターヴァイン農家", nameof(PopupTranslationPatch))]
    [TestCase("[■] 効果音", nameof(OptionsLocalizationPatch))]
    [TestCase("移動", nameof(OptionsLocalizationPatch))]
    [TestCase("新しいゲーム", "MainMenuLocalizationPatch > collection=LeftOptions > itemType=MainMenuOptionData > field=Text")]
    [TestCase("[基本盾]", nameof(GetDisplayNameProcessPatch))]
    [TestCase("[R]", nameof(CharGenLocalizationPatch))]
    [TestCase("[Delete]", nameof(CharGenLocalizationPatch))]
    [TestCase("[ ][3]", nameof(CharGenLocalizationPatch))]
    [TestCase("[■][2]", nameof(CharGenLocalizationPatch))]
    [TestCase("[2pts]", nameof(CharGenLocalizationPatch))]
    public void TranslatePreservingColors_SkipsAlreadyLocalizedDirectRouteText(string text, string context)
    {
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(text, context);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(text));
            Assert.That(Translator.GetMissingKeyHitCountForTests(text), Is.EqualTo(0));
        });
    }

    [TestCase("光学バイオスキャナ (Face)")]
    [TestCase("+1 Toughness")]
    [TestCase("Stinger (Confusing Venom)")]
    public void TranslatePreservingColors_KeepsObservingMixedLanguageDirectRouteText(string text)
    {
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(text, nameof(CharGenLocalizationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(text));
            Assert.That(Translator.GetMissingKeyHitCountForTests(text), Is.EqualTo(0),
                "Observation-only routes skip Translator.Translate entirely");
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsLevelExpHudLineUnchanged_ObservationOnly()
    {
        WriteDictionary(("LVL", "Lv"));

        var source = "LVL: 1 Exp: 0 / 220";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_NoTransformProbe_ForLevelExpSinkRoute_ObservationOnly()
    {
        WriteDictionary(("LVL", "Lv"));

        var source = "LVL: 1 Exp: 0 / 220";
        var output = TestTraceHelper.CaptureTrace(() =>
            Assert.That(
                UITextSkinTranslationPatch.TranslatePreservingColors(
                    source,
                    nameof(UITextSkinTranslationPatch)),
                Is.EqualTo(source)));

        Assert.That(output, Does.Not.Contain("DynamicTextProbe/v1"),
            "Observation-only routes do not emit transform probes");
    }

    [Test]
    public void TranslatePreservingColors_ReturnsHpHudLineUnchanged_ObservationOnly()
    {
        var source = "HP: 18 / 18";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsStatusLineUnchanged_ObservationOnly()
    {
        WriteDictionary(("Sated", "満腹"), ("Quenched", "潤っている"));

        var source = "Sated Quenched";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsActiveEffectsLineUnchanged_ObservationOnly()
    {
        WriteDictionary(
            ("ACTIVE EFFECTS:", "発動中の効果:"),
            ("wading", "浅瀬を進んでいる"),
            ("wet", "濡れている"));

        var source = "ACTIVE EFFECTS: wading, wet";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsExactLookupTextUnchanged_ObservationOnly()
    {
        WriteDictionary(
            ("take all", "すべて取る"),
            ("display options", "表示オプション"));

        var takeAll = UITextSkinTranslationPatch.TranslatePreservingColors(
            "take all",
            nameof(UITextSkinTranslationPatch));
        var displayOptions = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Display Options",
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(takeAll, Is.EqualTo("take all"));
            Assert.That(displayOptions, Is.EqualTo("Display Options"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("take all"), Is.EqualTo(0));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Display Options"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsThreatLineUnchanged_ObservationOnly()
    {
        WriteDictionary(("Perfect", "完璧"), ("Injured", "負傷"), ("Hostile", "敵対的"), ("Average", "平均"));

        var source = "Injured, Hostile, Average";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsCompareStatusLinesUnchanged_ObservationOnly()
    {
        WriteDictionary(
            ("Strength", "筋力"),
            ("Bonus Cap:", "ボーナス上限:"),
            ("Weapon Class:", "武器カテゴリ:"),
            ("Long Blades (increased penetration on critical hit)", "長剣（クリティカル時に貫通力上昇）"),
            ("no limit", "なし"));

        var capSource = "Strength Bonus Cap: 1";
        var egoCapSource = "Ego Bonus Cap: 2";
        var noLimitSource = "Strength Bonus Cap: no limit";
        var weaponClassSource = "Weapon Class: Long Blades (increased penetration on critical hit)";

        Assert.Multiple(() =>
        {
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(capSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(capSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(egoCapSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(egoCapSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(noLimitSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(noLimitSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(weaponClassSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(weaponClassSource));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesPickTargetCommandBarOwnerRoute()
    {
        WriteDictionaryFile(
            "ui-default.ja.json",
            ("lock", "ロック"));
        WriteDictionaryFile(
            "ui-pick-target.ja.json",
            ("Pick Target", "対象を選択"),
            ("Look", "見る"),
            ("interact", "インタラクト"),
            ("walk", "歩く"),
            ("select", "選択"),
            ("unlock", "固定解除"),
            ("[Select a direction]", "[方向を選択]"));
        WriteDictionaryFile(
            "ui-skillsandpowers.ja.json",
            ("Shield Slam", "シールドスラム"));

        var source = "Look | ESC | (F1) lock | space interact | W walk | Enter-select | Shield Slam | [Select a direction]";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(PickTargetWindowTextTranslator));

        Assert.That(
            translated,
            Is.EqualTo("見る | ESC | (F1) ロック | space インタラクト | W 歩く | Enter-選択 | シールドスラム | [方向を選択]"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesPickDirectionAbilityLabelAtTextSink()
    {
        WriteContextDictionaryFile(
            "ui-pick-target.ja.json",
            ("[Select a direction]", "PickTarget.DirectionPrompt", "[方向を選択]"));
        WriteContextDictionaryFile(
            "ui-skillsandpowers.ja.json",
            ("Discharge", "AbilityBar.ButtonText", "放電"));

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Discharge | [Select a direction]",
            nameof(UITextSkinTranslationPatch));

        Assert.That(translated, Is.EqualTo("放電 | [方向を選択]"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesRuntimeObservedPickTargetCommandBarMarkup()
    {
        WriteDictionaryFile(
            "ui-pick-target.ja.json",
            ("Pick Target", "対象を選択"),
            ("select", "選択"),
            ("unlock", "固定解除"));

        var source = "Pick Target | {{W|Space}}-select | unlock ({{hotkey|F1}}))";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(PickTargetWindowTextTranslator));

        Assert.That(translated, Is.EqualTo("対象を選択 | {{W|Space}}-選択 | 固定解除 ({{hotkey|F1}}))"));
    }

    [Test]
    public void TranslatePreservingColors_LeavesLookPickTargetCommandBarToOwnerRouteAtTextSink()
    {
        WriteDictionaryFile(
            "ui-default.ja.json",
            ("lock", "ロック"));
        WriteDictionaryFile(
            "ui-pick-target.ja.json",
            ("Look", "見る"),
            ("interact", "操作する"),
            ("walk", "歩く"));

        var source = "Look | ESC | (F1) lock | space interact | W walk";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(UITextSkinTranslationPatch));

        Assert.That(translated, Is.EqualTo(source));
    }

    [Test]
    public void TranslatePreservingColors_DoesNotUseActivatedAbilityNamesForStandalonePickTargetLabels()
    {
        WriteDictionaryFile(
            "ui-skillsandpowers.ja.json",
            ("Shield Slam", "シールドスラム"));

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            "Shield Slam",
            nameof(PickTargetWindowTextTranslator));

        Assert.That(translated, Is.EqualTo("Shield Slam"));
    }

    [Test]
    public void TranslatePreservingColors_PreservesOwnerRouteTokensInMarkupWrappedPickTargetCommandBarLabels()
    {
        WriteDictionary(("Reload", "リロード"));

        var source = "{{W|r}}-Reload | ESC";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(PickTargetWindowTextTranslator));

        Assert.That(translated, Is.EqualTo(source));
    }

    [TestCase("navigate", "移動")]
    [TestCase("Navigate", "移動")]
    [TestCase("select", "選択")]
    [TestCase("Select", "選択")]
    [TestCase("{{W|navigate}}", "{{W|移動}}")]
    public void TranslatePreservingColors_TranslatesDirectUiActionTokenAtTextSink(string source, string expected)
    {
        WriteDictionaryFile(
            "ui-default.ja.json",
            ("navigate", "移動"),
            ("select", "選択"));

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(UITextSkinTranslationPatch));

        Assert.That(translated, Is.EqualTo(expected));
    }

    [TestCase("{{W|space}}-select | unlock ({{hotkey|F1}}) | Fire Missile Weapon", "{{W|space}}-選択 | ロック解除 ({{hotkey|F1}}) | 飛び道具を射撃")]
    [TestCase("{{W|space}}-select | lock ({{hotkey|F1}}) | Fire Missile Weapon", "{{W|space}}-選択 | ロック ({{hotkey|F1}}) | 飛び道具を射撃")]
    [TestCase("{{W|space}}-select | (F1) {{W|lock}} | Fire Missile Weapon", "{{W|space}}-選択 | (F1) {{W|ロック}} | 飛び道具を射撃")]
    [TestCase("{{W|space}}-select | {{W|lock}} ({{hotkey|F1}}) | Fire Missile Weapon", "{{W|space}}-選択 | {{W|ロック}} ({{hotkey|F1}}) | 飛び道具を射撃")]
    [TestCase("{{W|space}}-select | R reload | unlock ({{hotkey|F1}})", "{{W|space}}-選択 | R reload | ロック解除 ({{hotkey|F1}})")]
    [TestCase("{{W|space}}-select | reload ({{hotkey|R}}) | unlock ({{hotkey|F1}})", "{{W|space}}-選択 | reload ({{hotkey|R}}) | ロック解除 ({{hotkey|F1}})")]
    [TestCase("{{W|space}}-select | R-reload | unlock ({{hotkey|F1}})", "{{W|space}}-選択 | R-reload | ロック解除 ({{hotkey|F1}})")]
    [TestCase("{{W|space}}-select | (R) {{W|Reload}} | unlock ({{hotkey|F1}})", "{{W|space}}-選択 | (R) {{W|Reload}} | ロック解除 ({{hotkey|F1}})")]
    public void TranslatePreservingColors_TranslatesPickTargetMissileFooterFromUiDictionary(string source, string expected)
    {
        WriteDictionaryFile(
            "ui-default.ja.json",
            ("lock", "ロック"),
            ("reload", "リロード"));
        WriteDictionaryFile(
            "ui-pick-target.ja.json",
            ("Fire Missile Weapon", "飛び道具を射撃"),
            ("select", "選択"),
            ("unlock", "ロック解除"));

        string translated = null!;
        var output = TestTraceHelper.CaptureTrace(() =>
        {
            translated = UITextSkinTranslationPatch.TranslatePreservingColors(
                source,
                nameof(UITextSkinTranslationPatch));
        });

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(expected));
            Assert.That(output, Does.Contain("PickTarget.UiText"));
        });
    }

    [Test]
    public void TranslatePreservingColors_LeavesPickTargetCommandBarUnchanged_WhenOwnerTokenMissing()
    {
        WriteDictionaryFile(
            "ui-default.ja.json",
            ("lock", "ロック"));
        WriteDictionaryFile(
            "ui-pick-target.ja.json",
            ("unlock", "ロック解除"));

        var source = "{{W|space}}-select | unlock ({{hotkey|F1}}) | Fire Missile Weapon";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(UITextSkinTranslationPatch));

        Assert.That(translated, Is.EqualTo(source));
    }

    [Test]
    public void MissileWeaponAreaTranslateLiteral_TranslatesAfterRenderHotkeySuffixes_FromOwnerDictionary()
    {
        WriteDictionaryFile(
            "ui-missile-weapon-area.ja.json",
            ("fire", "射撃"),
            ("reload", "リロード"));

        Assert.Multiple(() =>
        {
            Assert.That(MissileWeaponAreaTranslationPatch.TranslateLiteral("]}} fire"), Is.EqualTo("]}} 射撃"));
            Assert.That(MissileWeaponAreaTranslationPatch.TranslateLiteral("]}} reload"), Is.EqualTo("]}} リロード"));
            Assert.That(MissileWeaponAreaTranslationPatch.TranslateLiteral("{{K|You have no missile weapons equipped.}}"), Is.EqualTo("{{K|You have no missile weapons equipped.}}"));
        });
    }

    [Test]
    public void MissileWeaponAreaTranslateLiteral_LeavesHotkeySuffixesUnchanged_WhenOwnerDictionaryMissingKeys()
    {
        WriteDictionaryFile("ui-missile-weapon-area.ja.json");

        Assert.Multiple(() =>
        {
            Assert.That(MissileWeaponAreaTranslationPatch.TranslateLiteral("]}} fire"), Is.EqualTo("]}} fire"));
            Assert.That(MissileWeaponAreaTranslationPatch.TranslateLiteral("]}} reload"), Is.EqualTo("]}} reload"));
        });
    }

    [Test]
    public void MissileWeaponAreaPostfix_LeavesProducerTranslatedHotkeyLabelsUnchanged()
    {
        WriteDictionaryFile(
            "ui-missile-weapon-area.ja.json",
            ("fire", "射撃"),
            ("reload", "リロード"));

        var fire = new DummyUITextSkin();
        var reload = new DummyUITextSkin();
        fire.SetText("{{W|[F]}} 射撃");
        reload.SetText("{{W|[R]}} リロード");

        var output = TestTraceHelper.CaptureTrace(() => MissileWeaponAreaTranslationPatch.Postfix(fire, reload));

        Assert.Multiple(() =>
        {
            Assert.That(fire.text, Is.EqualTo("{{W|[F]}} 射撃"));
            Assert.That(reload.text, Is.EqualTo("{{W|[R]}} リロード"));
            Assert.That(fire.SetTextCallCount, Is.EqualTo(1));
            Assert.That(reload.SetTextCallCount, Is.EqualTo(1));
            Assert.That(output, Does.Not.Contain("MissileWeaponArea.HotkeyLabel"));
        });
    }

    [Test]
    public void MissileWeaponAreaPostfix_TranslatesPlayerUiFireAndReloadHotkeyLabels_FromOwnerDictionary()
    {
        WriteDictionaryFile(
            "ui-missile-weapon-area.ja.json",
            ("fire", "射撃"),
            ("reload", "リロード"));

        var fire = new DummyUITextSkin();
        var reload = new DummyUITextSkin();
        fire.SetText("{{W|[F]}} fire");
        reload.SetText("{{W|[R]}} reload");

        MissileWeaponAreaTranslationPatch.Postfix(fire, reload);

        Assert.Multiple(() =>
        {
            Assert.That(fire.text, Is.EqualTo("{{W|[F]}} 射撃"));
            Assert.That(reload.text, Is.EqualTo("{{W|[R]}} リロード"));
            Assert.That(fire.SetTextCallCount, Is.EqualTo(1));
            Assert.That(reload.SetTextCallCount, Is.EqualTo(1));
            Assert.That(fire.ApplyCallCount, Is.EqualTo(0));
            Assert.That(reload.ApplyCallCount, Is.EqualTo(0));
        });

        fire.Apply();
        reload.Apply();

        Assert.Multiple(() =>
        {
            Assert.That(fire.Text, Is.EqualTo("{{W|[F]}} 射撃"));
            Assert.That(reload.Text, Is.EqualTo("{{W|[R]}} リロード"));
        });
    }

    [Test]
    public void MissileWeaponAreaPostfix_LeavesHotkeyLabelsUnchanged_WhenOwnerDictionaryMissingKeys()
    {
        WriteDictionaryFile("ui-missile-weapon-area.ja.json");

        var fire = new DummyUITextSkin();
        var reload = new DummyUITextSkin();
        fire.SetText("{{W|[F]}} fire");
        reload.SetText("{{W|[R]}} reload");

        MissileWeaponAreaTranslationPatch.Postfix(fire, reload);

        Assert.Multiple(() =>
        {
            Assert.That(fire.Text, Is.EqualTo("{{W|[F]}} fire"));
            Assert.That(reload.Text, Is.EqualTo("{{W|[R]}} reload"));
        });
    }

    [Test]
    public void MissileWeaponAreaPostfix_LeavesEmptyAndDirectMarkerTextUnchanged()
    {
        WriteDictionaryFile(
            "ui-missile-weapon-area.ja.json",
            ("fire", "射撃"),
            ("reload", "リロード"));

        var fire = new DummyUITextSkin();
        var reload = new DummyUITextSkin();
        fire.SetText(string.Empty);
        reload.SetText(MessageFrameTranslator.MarkDirectTranslation("{{W|[R]}} reload"));

        MissileWeaponAreaTranslationPatch.Postfix(fire, reload);

        Assert.Multiple(() =>
        {
            Assert.That(fire.Text, Is.EqualTo(string.Empty));
            Assert.That(reload.Text, Is.EqualTo(MessageFrameTranslator.MarkDirectTranslation("{{W|[R]}} reload")));
        });
    }

    [Test]
    public void MissileWeaponAreaPostfix_LeavesOtherFireAndReloadContextsUnchanged()
    {
        WriteDictionaryFile(
            "ui-missile-weapon-area.ja.json",
            ("fire", "射撃"),
            ("reload", "リロード"));

        var fire = new DummyUITextSkin();
        var reload = new DummyUITextSkin();
        fire.SetText("{{W|[F]}} fire mode");
        reload.SetText("{{W|[R]}} Reload from checkpoint");

        MissileWeaponAreaTranslationPatch.Postfix(fire, reload);

        Assert.Multiple(() =>
        {
            Assert.That(fire.Text, Is.EqualTo("{{W|[F]}} fire mode"));
            Assert.That(reload.Text, Is.EqualTo("{{W|[R]}} Reload from checkpoint"));
        });
    }

    [Test]
    public void TranslatePreservingColors_LeavesDirectUiActionTokenUnchangedForOwnerObservationContext()
    {
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            "select",
            nameof(PickTargetWindowTextTranslator));

        Assert.That(translated, Is.EqualTo("select"));
    }

    [Test]
    public void TranslatePreservingColors_ReturnsSourceUnchangedForInventoryObservationOnlyRoute()
    {
        WriteDictionary(("Show Tooltip", "ツールチップ表示"));

        var source = "Show Tooltip";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(InventoryAndEquipmentStatusScreenTranslationPatch));

        Assert.That(translated, Is.EqualTo(source),
            "InventoryAndEquipmentStatusScreenTranslationPatch is observation-only — source must pass through unchanged");
    }

    [Test]
    public void TranslatePreservingColors_ReturnsFactionWrappersUnchanged_ObservationOnly()
    {
        WriteDictionary(
            ("The villagers of {0} don't care about you, but aggressive ones will attack you.", "{0}の村人たちはあなたを特に気に掛けていないが、攻撃的な者は襲ってくる。"),
            ("The {0}", "{0}"),
            ("Reputation: {0}", "評判: {0}"));

        var relationshipSource = "The villagers of Abal don't care about you, but aggressive ones will attack you.";
        var labelSource = "The Arbitrarilyborn Cult";
        var reputationSource = "Reputation:     0";

        Assert.Multiple(() =>
        {
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(relationshipSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(relationshipSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(labelSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(labelSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(reputationSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(reputationSource));
        });
    }

    [Test]
    public void TranslatePreservingColors_SkipsFactionsLineOwnedTextWithoutMissingKeyLogs()
    {
        var source = "The villagers of Abal don't care about you, but aggressive ones will attack you.";

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(FactionsLineTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsSkillStatusFamiliesUnchanged_ObservationOnly()
    {
        WriteDictionary(
            ("Learned", "習得済み"),
            ("Starting Cost [{val} sp]", "初期コスト [{val} sp]"),
            ("Skill Points (SP): {val}", "スキルポイント (SP): {val}"),
            ("Tinker I", "工匠 I"),
            ("Tinker II", "工匠 II"));

        var learnedSource = "Learned [5/10]";
        var startingCostSource = "Starting Cost [100 sp] [1/10]";
        var requirementBlockSource = ":: 100 SP ::\n:: 23 Intelligence ::\n";
        var skillLineSource = "    :Tinker II [200sp] 23 Intelligence, Tinker I";

        Assert.Multiple(() =>
        {
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(learnedSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(learnedSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(startingCostSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(startingCostSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(requirementBlockSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(requirementBlockSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(skillLineSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(skillLineSource));
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsSkillSectionLabelsUnchanged_ObservationOnly()
    {
        WriteDictionary(
            ("Melee", "近接戦闘"),
            ("Melee Weapons", "近接武器"),
            ("Short Blades", "短剣"));

        var sectionSource = ":Melee";
        var subsectionSource = "  :Melee Weapons";
        var skillLineSource = "    :Short Blades [100sp] 15 Agility";

        Assert.Multiple(() =>
        {
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(sectionSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(sectionSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(subsectionSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(subsectionSource));
            Assert.That(UITextSkinTranslationPatch.TranslatePreservingColors(skillLineSource, nameof(UITextSkinTranslationPatch)), Is.EqualTo(skillLineSource));
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsTrimmedLookupTextUnchanged_ObservationOnly()
    {
        WriteDictionary(("Joppa", "ジョッパ"));

        var source = " Joppa";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(UITextSkinTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [TestCase(nameof(UITextSkinTranslationPatch), nameof(CharGenLocalizationPatch), "QudMutationsModule", "QudCyberneticsModule")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(CharGenLocalizationPatch), "EmbarkBuilder")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(CharacterStatusScreenTranslationPatch), "Qud.UI.CharacterStatusScreen")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(CharacterStatusScreenTranslationPatch), "Qud.UI.CharacterMutationLine")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(CharacterStatusScreenTranslationPatch), "Qud.UI.CharacterAttributeLine")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(FactionsStatusScreenTranslationPatch), "Qud.UI.FactionsStatusScreen")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(FactionsLineTranslationPatch), "Qud.UI.FactionsLine")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(MainMenuLocalizationPatch), "Qud.UI.MainMenu")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(OptionsLocalizationPatch), "Qud.UI.OptionsScreen")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(PickTargetWindowTextTranslator), "XRL.UI.PickTargetWindow")]
    [TestCase(nameof(UITextSkinTranslationPatch), nameof(PopupTranslationPatch), "Qud.UI.Popup")]
    public void ResolveObservabilityContext_ReclassifiesKnownSinkStacks(
        string originalContext,
        string expectedContext,
        params string[] stackTypeNames)
    {
        var resolvedContext = UITextSkinTranslationPatch.ResolveObservabilityContextForTests(originalContext, stackTypeNames);

        Assert.That(resolvedContext, Is.EqualTo(expectedContext));
    }

    [TestCase("Points Remaining: 12")]
    [TestCase("Your Strength score determines how effectively you penetrate armor.")]
    [TestCase("ù +2 Ego\nù Proselytize\n")]
    public void ResolveObservabilityContext_ReclassifiesKnownCharGenTextPatterns(string source)
    {
        var resolvedContext = UITextSkinTranslationPatch.ResolveObservabilityContextForTests(
            nameof(UITextSkinTranslationPatch),
            source,
            "Some.Unrelated.Widget");

        Assert.That(resolvedContext, Is.EqualTo(nameof(CharGenLocalizationPatch)));
    }

    [Test]
    public void ResolveObservabilityContext_LeavesUnknownSinkStackUntouched()
    {
        var resolvedContext = UITextSkinTranslationPatch.ResolveObservabilityContextForTests(
            nameof(UITextSkinTranslationPatch),
            "Some.Unrelated.Widget");

        Assert.That(resolvedContext, Is.EqualTo(nameof(UITextSkinTranslationPatch)));
    }

    [Test]
    public void TranslatePreservingColors_ReturnsSourceUnchangedForFactionsObservationOnlyRoute()
    {
        WriteDictionary(
            ("The villagers of {0} don't care about you, but aggressive ones will attack you.", "{0}の村人たちはあなたを特に気に掛けていないが、攻撃的な者は襲ってくる。"));

        var source = "The villagers of Abal don't care about you, but aggressive ones will attack you.";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(FactionsStatusScreenTranslationPatch));

        Assert.That(translated, Is.EqualTo(source),
            "FactionsStatusScreenTranslationPatch is observation-only — source must pass through unchanged");
    }

    [Test]
    public void TranslatePreservingColors_ReturnsSourceUnchangedForFactionsTopicListsObservationOnlyRoute()
    {
        WriteDictionary(
            ("The {0} are interested in learning about {1}.", "{0}は{1}について知ることに関心がある。"),
            ("the locations of insect lair", "昆虫の巣の場所"),
            ("the locations of ape lair", "類人猿の巣の場所"),
            ("sultan they admire or despise", "彼らが好悪を抱くスルタン"),
            ("gossip that's about them", "彼ら自身に関するうわさ話"));

        var source = "The apes are interested in learning about the locations of insect lair, the locations of ape lair, sultan they admire or despise, and gossip that's about them.";
        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(FactionsStatusScreenTranslationPatch));

        Assert.That(translated, Is.EqualTo(source),
            "FactionsStatusScreenTranslationPatch is observation-only — source must pass through unchanged");
    }

    [Test]
    public void TranslatePreservingColors_ReturnsSourceUnchangedForCharacterStatusObservationOnlyRoute()
    {
        WriteDictionary(
            ("Attribute Points: {0}", "能力値ポイント: {0}"),
            ("Mutated Human", "変異人間"));

        var sources = new[]
        {
            "Attribute Points: 0",
            "Mutation Points: 0",
            "Mutated Human Tinker",
            "Level: 1 ¯ HP: 18/18 ¯ XP: 0/220 ¯ Weight: 405#",
            "Force Wall (1)",
            "{{G|RANK 1/10}}",
            "{{c|[Mental Mutation]}}",
            "You see in the dark.",
        };

        Assert.Multiple(() =>
        {
            foreach (var source in sources)
            {
                var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
                    source,
                    nameof(CharacterStatusScreenTranslationPatch));

                Assert.That(translated, Is.EqualTo(source),
                    $"CharacterStatusScreenTranslationPatch is observation-only — '{source}' must pass through unchanged");
            }
        });
    }

    [Test]
    public void TranslatePreservingColors_ReturnsSourceUnchangedForCharacterStatusCompactLinesObservationOnlyRoute()
    {
        WriteDictionary(
            ("LVL", "Lv"),
            ("ACTIVE EFFECTS:", "発動中の効果:"),
            ("wading", "浅瀬を進んでいる"),
            ("wet", "濡れている"));

        var sources = new[]
        {
            "LVL: 1 Exp: 0 / 220",
            "HP: 18 / 18",
            "ACTIVE EFFECTS: wading, wet",
            "Strength Bonus Cap: no limit",
            "Ego Bonus Cap: 2",
            "Weapon Class: Long Blades (increased penetration on critical hit)",
        };

        Assert.Multiple(() =>
        {
            foreach (var source in sources)
            {
                var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
                    source,
                    nameof(CharacterStatusScreenTranslationPatch));

                Assert.That(translated, Is.EqualTo(source),
                    $"CharacterStatusScreenTranslationPatch is observation-only — '{source}' must pass through unchanged");
            }
        });
    }

    [Test]
    public void TranslateStringField_ObservationOnly_LeavesKnownTextUnchanged()
    {
        WriteDictionary(("New Game", "新しいゲーム"));

        var option = new DummyMenuOption { Text = "New Game" };

        UITextSkinTranslationPatch.TranslateStringField(
            option,
            nameof(DummyMenuOption.Text),
            "MainMenuLocalizationPatch > collection=LeftOptions");

        Assert.Multiple(() =>
        {
            Assert.That(option.Text, Is.EqualTo("New Game"));
            Assert.That(SinkObservation.GetHitCountForTests(
                nameof(UITextSkinTranslationPatch),
                "MainMenuLocalizationPatch > collection=LeftOptions",
                SinkObservation.ObservationOnlyDetail,
                "New Game",
                "New Game"), Is.GreaterThan(0));
        });
    }

    [Test]
    public void TranslateStringField_PassesThroughUnknownText()
    {
        WriteDictionary(("Known", "既知"));

        var option = new DummyMenuOption { Text = "Unknown text" };

        UITextSkinTranslationPatch.TranslateStringField(
            option,
            nameof(DummyMenuOption.Text),
            "MainMenuLocalizationPatch > collection=LeftOptions");

        Assert.That(option.Text, Is.EqualTo("Unknown text"),
            "Unknown text passes through unchanged when not in dictionary");
    }

    [Test]
    public void TranslatePreservingColors_SuppressesAlreadyLocalizedMarkupWrappedJapaneseDisplayName()
    {
        var source = "{{B}}|濡れた豚農家";

        var translated = UITextSkinTranslationPatch.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test, Category("L2")]
    [TestCase("MainMenuLocalizationPatch")]
    [TestCase("OptionsLocalizationPatch")]
    [TestCase("CharGenLocalizationPatch")]
    [TestCase("PickTargetWindowTextTranslator")]
    [TestCase("CharacterStatusScreenTranslationPatch")]
    [TestCase("FactionsStatusScreenTranslationPatch")]
    [TestCase("InventoryAndEquipmentStatusScreenTranslationPatch")]
    [TestCase("ConversationDisplayTextPatch")]
    [TestCase("DescriptionLongDescriptionPatch")]
    [TestCase("LookTooltipContentPatch")]
    [TestCase("UITextSkinTranslationPatch")]
    public void TranslatePreservingColors_AllRoutes_ObservationOnly(string context)
    {
        SinkObservation.ResetForTests();
        var source = "English text that would normally be translated";
        var result = UITextSkinTranslationPatch.TranslatePreservingColors(source, context);
        Assert.That(result, Is.EqualTo(source));
    }

    [Test]
    public void Prefix_HandlesNullOrEmpty()
    {
        WriteDictionary(("Hello", "こんにちは"));

        var emptyText = string.Empty;
        UITextSkinTranslationPatch.Prefix(ref emptyText);

        Assert.That(emptyText, Is.EqualTo(string.Empty));
    }

    [Test]
    public void HarmonyPatch_AppliesPrefix_ToDummyUITextSkin_ObservationOnly()
    {
        WriteDictionary(("World", "世界"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyUITextSkin), nameof(DummyUITextSkin.SetText)),
                prefix: new HarmonyMethod(RequireMethod(typeof(UITextSkinTranslationPatch), nameof(UITextSkinTranslationPatch.Prefix))));

            var dummy = new DummyUITextSkin();
            dummy.SetText("World");

            Assert.That(dummy.Text, Is.EqualTo("World"),
                "Observation-only mode passes source through unchanged");
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static string CreateHarmonyId()
    {
        return $"qudjp.tests.{Guid.NewGuid():N}";
    }

    private static MethodInfo RequireMethod(Type type, string methodName)
    {
        return AccessTools.Method(type, methodName)
            ?? throw new InvalidOperationException($"Method not found: {type.FullName}.{methodName}");
    }

    private sealed class DummyMenuOption
    {
        public string Text = string.Empty;
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        WriteDictionaryFile("ui-textskin.ja.json", entries);
    }

    private void WriteDictionaryFile(string fileName, params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"entries\":[");

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

        var path = Path.Combine(tempDirectory, fileName);
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        ScopedDictionaryLookup.ResetForTests();
    }

    private void WriteContextDictionaryFile(
        string fileName,
        params (string key, string? context, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"entries\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(entries[index].key));
            builder.Append('"');
            if (!string.IsNullOrWhiteSpace(entries[index].context))
            {
                builder.Append(",\"context\":\"");
                builder.Append(EscapeJson(entries[index].context!));
                builder.Append('"');
            }

            builder.Append(",\"text\":\"");
            builder.Append(EscapeJson(entries[index].text));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        var path = Path.Combine(tempDirectory, fileName);
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        ScopedDictionaryLookup.ResetForTests();
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
