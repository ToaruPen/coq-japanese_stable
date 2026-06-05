using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class GetDisplayNameRouteTranslatorTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-displayname-route-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(tempDirectory);
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        LocalizationAssetResolver.SetLocalizationRootForTests(null);
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TranslatePreservingColors_PreservesQudWrapperMarkup()
    {
        WriteDictionary(("water flask", "水袋"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{C|water flask x2}}",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{C|水袋 x2}}"));
    }

    [Test]
    public void TranslateScopedExactPreservingColors_ReturnsEmptyAndLogsWarning_WhenSourceIsNull()
    {
        var output = TestTraceHelper.CaptureTrace(() =>
            Assert.That(GetDisplayNameRouteTranslator.TranslateScopedExactPreservingColors(null), Is.EqualTo(string.Empty)));

        Assert.That(output, Does.Contain("TranslateScopedExactPreservingColors received null source"));
    }

    [Test]
    public void TranslateScopedExactPreservingColors_ReturnsEmptyWithoutWarning_WhenSourceIsEmpty()
    {
        var output = TestTraceHelper.CaptureTrace(() =>
            Assert.That(GetDisplayNameRouteTranslator.TranslateScopedExactPreservingColors(string.Empty), Is.EqualTo(string.Empty)));

        Assert.That(output, Is.Empty);
    }

    [Test]
    public void TranslatePreservingColors_UsesDisplayNameScopedBracketedStateLookups()
    {
        WriteDictionary(("water flask", "水袋"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[empty]", "[空]"),
            ("[empty, sealed]", "[空／密封]"),
            ("[sealed]", "[密封]"),
            ("[auto-collecting]", "[自動採取中]"));

        Assert.Multiple(() =>
        {
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "<color=#44ff88>water flask [empty]</color>",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("<color=#44ff88>水袋 [空]</color>"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "<color=#44ff88>water flask [empty, sealed]</color>",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("<color=#44ff88>水袋 [空／密封]</color>"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "<color=#44ff88>water flask [auto-collecting]</color>",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("<color=#44ff88>水袋 [自動採取中]</color>"));
        });
    }

    [Test]
    public void TranslatePreservingColors_UsesProductionAliasForLegacySpaserDisplayNameWithLeadingModifier()
    {
        UseProductionDictionaries();

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{c|jacked}} {{spaser|スパーザー}}ライフル",
            nameof(GetDisplayNameProcessPatch));

        Assert.That(translated, Is.EqualTo("{{c|ジャック付き}} {{spaser|スペーザー}}ライフル"));
    }

    [Test]
    public void TranslatePreservingColors_UsesDisplayNameAliasForLegacySpaserDisplayNameWithWeaponStats()
    {
        WriteAliasFile(
            ("スパーザーライフル", "{{spaser|スペーザー}}ライフル"));
        WriteDictionaryFile("ui-displayname-adjectives.ja.json", ("[no cell]", "[セルなし]"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "スパーザーライフル \u001A14 \u00031d12 [no cell] <AAC7>",
            nameof(GetDisplayNameProcessPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.Contain("スペーザー"));
            Assert.That(translated, Does.Contain("ライフル"));
            Assert.That(translated, Does.Contain("[セルなし]"));
            Assert.That(translated, Does.Not.Contain("スパーザー"));
        });
    }

    [Test]
    public void TranslatePreservingColors_ProductionAlias_UpdatesCachedSpaserTooltipWeaponName()
    {
        UseProductionDictionaries();

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "スパーザーライフル \u001A14 \u00031d12 [no cell] <AAC7>",
            nameof(LookTooltipInformationWrapPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.Contain("スペーザー"));
            Assert.That(translated, Does.Contain("ライフル"));
            Assert.That(translated, Does.Contain("[セルなし]"));
            Assert.That(translated, Does.Not.Contain("スパーザー"));
        });
    }

    [Test]
    public void TranslatePreservingColors_StripsEnglishArticleFromLocalizedGeneratedWeaponStatsName()
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "the 威厳ある嘆きの尖端 \u001A10/13 \u00031d12+1",
            nameof(LookTooltipInformationWrapPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.StartWith("威厳ある嘆きの尖端 "));
            Assert.That(translated, Does.Not.StartWith("the "));
            Assert.That(translated, Does.Contain("10"));
            Assert.That(translated, Does.Contain("1d12+1"));
        });
    }

    [Test]
    public void TranslatePreservingColors_StripsEnglishArticleFromColoredLocalizedGeneratedWeaponStatsName()
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "the {{Y-R-Y-Y-Y-Y-Y-r-Y sequence|威厳ある嘆きの尖端}} {{c|\u001A}}10{{K|/13}} {{r|\u0003}}1d12+1",
            nameof(LookTooltipInformationWrapPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.StartWith("{{Y-R-Y-Y-Y-Y-Y-r-Y sequence|威厳ある嘆きの尖端}} "));
            Assert.That(translated, Does.Not.StartWith("the "));
            Assert.That(translated, Does.Contain("{{c|\u001A}}10"));
            Assert.That(translated, Does.Contain("{{r|\u0003}}1d12+1"));
        });
    }

    [Test]
    public void TranslatePreservingColors_StripsEnglishArticleFromLocalizedGeneratedDisplayName()
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "the 威厳ある嘆きの尖端",
            nameof(GetDisplayNameProcessPatch));

        Assert.That(translated, Is.EqualTo("威厳ある嘆きの尖端"));
    }

    [Test]
    public void TranslatePreservingColors_StripsEnglishArticleFromColoredLocalizedGeneratedDisplayName()
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "the {{G|太陽の剣}}",
            nameof(GetDisplayNameProcessPatch));

        Assert.That(translated, Is.EqualTo("{{G|太陽の剣}}"));
    }

    [TestCase("some {{r|生の猪肉}}")]
    [TestCase("Some {{r|生の猪肉}}")]
    public void TranslatePreservingColors_StripsSomeArticleModifierFromLocalizedDisplayName(string source)
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{r|生の猪肉}}"));
    }

    [Test]
    public void TranslatePreservingColors_PreservesColorWrapperAroundAlreadyLocalizedDisplayName()
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{W|ヨンダーケーン}}",
            nameof(CampfirePreserveTranslationPatch));

        Assert.That(translated, Is.EqualTo("{{W|ヨンダーケーン}}"));
    }

    [Test]
    public void TranslatePreservingColors_UsesDisplayNameAliasForCachedTooltipWeaponName()
    {
        WriteAliasFile(
            ("旧式位相ライフル", "{{phase|新式位相ライフル}}"));
        WriteDictionaryFile("ui-displayname-adjectives.ja.json", ("[no cell]", "[セルなし]"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "旧式位相ライフル \u001A14 \u00031d12 [no cell] <AAC7>",
            nameof(LookTooltipInformationWrapPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.Contain("新式位相ライフル"));
            Assert.That(translated, Does.Contain("[セルなし]"));
            Assert.That(translated, Does.Not.Contain("旧式位相ライフル"));
        });
    }

    [Test]
    public void TranslatePreservingColors_ProductionDictionary_TranslatesWaterStainedPrefix()
    {
        UseProductionDictionaries();

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "water-stained chem cell",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("水染みのケムセル"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesHydraulicLiquidAndFlywheelSuffixes()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("solar", "太陽光"),
            ("{{W|solar}}", "{{W|太陽光}}"),
            ("{{c|sealed}}", "{{c|密封}}"));
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("pumping station", "ポンプステーション"),
            ("{{c|pumping station}}", "{{c|ポンプステーション}}"));
        WriteDictionaryFile(
            "ui-liquid-adjectives.ja.json",
            ("{{g|algal}}", "{{g|藻質の}}"));
        WriteDictionaryFile(
            "ui-liquids.ja.json",
            ("{{B|water}}", "{{B|水}}"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{W|solar}} {{c|pumping station}} {{y|[{{rules|256}} drams of {{g|algal}} {{B|water}}, {{c|sealed}}]}} {{y|(flywheel: {{G|Full Speed}})}}",
            nameof(GetDisplayNamePatch));

        Assert.That(
            translated,
            Is.EqualTo("{{W|太陽光}} {{c|ポンプステーション}} {{y|[{{rules|256}}ドラムの{{g|藻質の}}{{B|水}}、{{c|密封}}]}} {{y|(フライホイール: {{G|最高速}})}}"));
    }

    [Test]
    public void TranslatePreservingColors_StripsDirectMarkerFromFlywheelBracketedState()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("pumping station", "ポンプステーション"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "pumping station [flywheel: " + MessageFrameTranslator.DirectTranslationMarker + "Full Speed]",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("ポンプステーション [フライホイール: 最高速]"));
    }

    [Test]
    public void TranslatePreservingColors_StripsDirectMarkerFromUnknownFlywheelBracketedState()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("pumping station", "ポンプステーション"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "pumping station [flywheel: " + MessageFrameTranslator.DirectTranslationMarker + "SomeUnknownState]",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("ポンプステーション [フライホイール: SomeUnknownState]"));
    }

    [Test]
    public void TranslatePreservingColors_PreservesMarkupInsideWorshipperTitleSuffixTarget()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("broken", "破損"));
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("guard", "衛兵"),
            ("chrome pyramid", "クロムピラミッド"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "guard and worshipper of {{W|chrome pyramid}} [{{K|broken}}]",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("衛兵、{{W|クロムピラミッド}}の崇拝者 [{{K|破損}}]"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesMultipleLiquidAndStateSuffixes()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[auto-collecting]", "[自動収集中]"),
            ("[broken]", "[破損]"));
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("lead-acid cell", "鉛酸セル"));
        WriteDictionaryFile(
            "ui-liquids.ja.json",
            ("{{G|acid}}", "{{G|酸}}"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "lead-acid cell {{y|[{{rules|8}} drams of {{G|acid}}]}} {{y|[{{c|auto-collecting}}]}}",
            nameof(GetDisplayNamePatch));

        Assert.That(
            translated,
            Is.EqualTo("鉛酸セル {{y|[{{rules|8}}ドラムの{{G|酸}}]}} {{y|[{{c|自動収集中}}]}}"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesPlainMultipleLiquidAndStateSuffixes()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[auto-collecting]", "[自動収集中]"));
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("lead-acid cell", "鉛酸セル"));
        WriteDictionaryFile(
            "ui-liquids.ja.json",
            ("acid", "酸"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "lead-acid cell [8 drams of acid] [auto-collecting]",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("鉛酸セル [8ドラムの酸] [自動収集中]"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesHydraulicAndFusionModifiers()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("hydraulic", "油圧式"),
            ("{{C|fusion}}", "{{C|核融合式}}"));
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("cleaner", "洗浄器"),
            ("pumping station", "ポンプ場"));

        Assert.Multiple(() =>
        {
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "hydraulic cleaner [密封]",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("油圧式洗浄器 [密封]"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "{{C|fusion}} pumping station",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("{{C|核融合式}} ポンプ場"));
        });
    }

    [Test]
    public void TranslatePreservingColors_DoesNotSplitAndInsideBaseNameBeforeTitleSuffix()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("hand axe", "手斧"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "hand axe, 5th Edition",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("手斧、第5版"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesMarkedUpBaseBeforeGeneratedTitleSuffix()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("laser rifle", "レーザーライフル"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{W|laser rifle}}, 1st Edition",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{W|レーザーライフル}}、第1版"));
    }

    [Test]
    public void TranslatePreservingColors_DoesNotUseEarlyTitleSuffixRouteForMultiWrapperBase()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("water flask", "水袋"));
        WriteContextDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[empty]", "GetDisplayName.State", "[空]"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{W|water flask}} {{y|[empty]}}, 1st Edition",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{W|水袋}} {{y|[空]}}、第1版"));
    }

    [Test]
    public void TranslatePreservingColors_PreservesSingleMarkedUpBracketedStateSuffix()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("water flask", "水袋"));
        WriteContextDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[empty]", "GetDisplayName.State", "[空]"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "water flask {{y|[empty]}}",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("水袋 {{y|[空]}}"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesWholeWrappedBaseBeforeQuantifiedLiquidState()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("water flask", "水袋"));
        WriteDictionaryFile(
            "ui-liquids.ja.json",
            ("slime", "スライム"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{W|water flask}} {{y|[1 dram of slime]}}",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{W|水袋}} {{y|[1ドラムのスライム]}}"));
    }

    [Test]
    public void TranslatePreservingColors_DoesNotDuplicateColoredLiquidPrefixInLocalizedHead()
    {
        WriteDictionaryFile(
            "ui-liquids.ja.json",
            ("acid", "{{R|酸}}"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{R|酸}}の水たまり of acid",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{R|酸}}の水たまり"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesMarkedUpWorshipperTargetWithMarkedUpBracketState()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("guard", "衛兵"),
            ("water flask", "水袋"));
        WriteContextDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[empty]", "GetDisplayName.State", "[空]"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "guard and worshipper of {{W|water flask}} {{y|[empty]}}",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("衛兵、{{W|水袋}} {{y|[空]}}の崇拝者"));
    }

    [TestCase("guard and friend to {{R|water barons}}", "衛兵、{{R|水の男爵}}の友")]
    [TestCase("guard and member of {{R|water barons}}", "衛兵、{{R|水の男爵}}の一員")]
    [TestCase("guard and {{R|friend to water barons}}", "衛兵、{{R|水の男爵の友}}")]
    [TestCase("guard and {{R|member of water barons}}", "衛兵、{{R|水の男爵の一員}}")]
    public void TranslatePreservingColors_TranslatesMarkedUpSocialRoleTitleTarget(
        string source,
        string expected)
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("guard", "衛兵"),
            ("water barons", "水の男爵"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(expected));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesMarkedUpBaseAndStateSuffixSequence()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("chem cell", "ケムセル"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("sealed", "密封"));
        WriteDictionary(("fresh water", "真水"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{c|chem cell}} {{y|[{{B|fresh water}}]}} {{y|[{{c|sealed}}]}}",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{c|ケムセル}} {{y|[{{B|真水}}]}} {{y|[{{c|密封}}]}}"));
    }

    [Test]
    public void TranslatePreservingColors_PreservesArmorStatSymbolTags_WhenTranslatingNameAndState()
    {
        WriteDictionary(("dromad waterskin", "ラクダの水袋"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[empty]", "[空]"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "dromad waterskin {{b|\u0004}}0 {{K|\t}}0 [empty]",
            nameof(GetDisplayNamePatch));
        var alreadyLocalizedBase = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "ラクダの水袋 {{b|\u0004}}0 {{K|\t}}0 [empty]",
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("ラクダの水袋 {{b|\u0004}}0 {{K|\t}}0 [空]"));
            Assert.That(alreadyLocalizedBase, Is.EqualTo("ラクダの水袋 {{b|\u0004}}0 {{K|\t}}0 [空]"));
        });
    }

    [Test]
    public void TranslatePreservingColors_PreservesArmorStatDisplayNameColorOwnership()
    {
        WriteDictionary(("dromad waterskin", "ラクダの水袋"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("bloody", "{{r|血まみれの}}"),
            ("[empty]", "[空]"));

        Assert.Multiple(() =>
        {
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "<color=#44ff88>dromad waterskin {{b|\u0004}}1 {{K|\t}}-2 [empty]</color>",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("<color=#44ff88>ラクダの水袋 {{b|\u0004}}1 {{K|\t}}-2 [空]</color>"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "{{C|dromad waterskin}} {{b|\u0004}}-1 {{K|\t}}2",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("{{C|ラクダの水袋}} {{b|\u0004}}-1 {{K|\t}}2"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "{{r|bloody}} dromad waterskin {{b|\u0004}}0 {{K|\t}}0 [empty]",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("{{r|血まみれの}} ラクダの水袋 {{b|\u0004}}0 {{K|\t}}0 [空]"));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesMarkupWrappedStainedModifierBeforeLocalizedBase()
    {
        WriteContextDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("{{g|slime}}-stained", "GetDisplayName.Adjective", "{{g|スライム}}でぬめった"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{g|slime}}-stained 両手用{{b|カーバイドの長剣}}",
            nameof(GetDisplayNameProcessPatch));

        Assert.That(translated, Is.EqualTo("{{g|スライム}}でぬめった両手用{{b|カーバイドの長剣}}"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesOuterWrappedStainedModifierThroughLiquidRoute()
    {
        WriteDictionary(("sword", "剣"));
        WriteDictionaryFile("ui-liquids.ja.json", ("oil", "油"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{r|oil-stained}} sword",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{r|油}}に染まった剣"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesGeneratedCompoundStainedModifier()
    {
        WriteDictionary(("leather cap", "革の帽子"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("blood", "血"),
            ("slime", "粘液"),
            ("tar", "タール"),
            ("water", "水"));

        Assert.Multiple(() =>
        {
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "blood-and-tar-stained leather cap",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("血とタールで汚れた革の帽子"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "water-and-slime-stained leather cap",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("水と粘液で汚れた革の帽子"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "{{r|blood}}-and-{{g|slime}}-stained leather cap",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("{{r|血}}と{{g|粘液}}で汚れた革の帽子"));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesProductionColoredLiquidStainsBeforeLocalizedBase()
    {
        UseProductionDictionaries();

        Assert.Multiple(() =>
        {
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "{{g|slime}}-stained {{C|高級工具セット}}",
                    nameof(GetDisplayNameProcessPatch)),
                Is.EqualTo("{{g|粘液}}に染まった{{C|高級工具セット}}"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "{{G|goo}}-and-{{g|slime}}-stained {{Y|鋼鉄}}のブーツ",
                    nameof(GetDisplayNameProcessPatch)),
                Is.EqualTo("{{G|粘液}}と{{g|粘液}}で汚れた{{Y|鋼鉄}}のブーツ"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "slender {{g|slime}}-and-{{w|sludge}}-stained {{B|積層カーバイドの長剣}}",
                    nameof(GetDisplayNameProcessPatch)),
                Is.EqualTo("細身な {{g|粘液}}と{{w|汚泥}}で汚れた{{B|積層カーバイドの長剣}}"));
        });
    }

    [Test]
    public void TranslatePreservingColors_PreservesCompactWeaponStatSymbolTags_WhenTranslatingName()
    {
        WriteDictionary(
            ("bronze sword", "青銅の剣"),
            ("throwing axe", "投げ斧"),
            ("arc cannon", "アークキャノン"));

        Assert.Multiple(() =>
        {
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "bronze sword {{c|\u001a}}5{{K|/7}} {{r|\u0003}}1d6",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("青銅の剣 {{c|\u001a}}5{{K|/7}} {{r|\u0003}}1d6"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "{{C|throwing axe {{c|\u001a}}÷+1 {{r|\u0003}}1d4}}",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("{{C|投げ斧 {{c|\u001a}}÷+1 {{r|\u0003}}1d4}}"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "arc cannon {{r|\u0003}}2d6",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("アークキャノン {{r|\u0003}}2d6"));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesCompactWeaponTrailingStatesAfterAmmoAndAngleCode()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[rusted]", "[{{r|錆びた}}]"),
            ("[broken]", "[{{r|破損}}]"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "クローム・リボルバー {{c|\u001a}}7 {{r|\u0003}}1d6 {{y|[鉛スラッグ x6]}} [{{r|rusted}}] [{{r|broken}}] {{y|<{{B|C}}{{B|C}}{{g|2}}>}}",
            nameof(GetDisplayNamePatch));

        Assert.That(
            translated,
            Is.EqualTo("クローム・リボルバー {{c|\u001a}}7 {{r|\u0003}}1d6 {{y|[鉛スラッグ x6]}} [{{r|錆びた}}] [{{r|破損}}] {{y|<{{B|C}}{{B|C}}{{g|2}}>}}"));
    }

    [Test]
    public void TranslatePreservingColors_RestoresCompactWeaponStatTags_FromRuntimeControlCodes()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[rusted]", "[{{r|錆びた}}]"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "クローム・リボルバー \u001a7 \u00031d6 [鉛スラッグ x6] [rusted] <CC2>",
            nameof(GetDisplayNamePatch));

        Assert.That(
            translated,
            Is.EqualTo("クローム・リボルバー {{c|\u001a}}7 {{r|\u0003}}1d6 {{y|[鉛スラッグ x6]}} [{{r|錆びた}}] {{y|<{{B|C}}{{B|C}}{{g|2}}>}}"));
    }

    [TestCase(
        "{{c|ケムセル}} [{{r|rusted}}] {{y|<{{G|B}}{{C|D}}{{r|1}}>}}",
        "{{c|ケムセル}} [{{r|錆びた}}] {{y|<{{G|B}}{{C|D}}{{r|1}}>}}")]
    [TestCase(
        "{{c|ケムセル}} [{{r|錆びた}}] {{y|<{{G|B}}{{C|D}}{{r|1}}>}}",
        "{{c|ケムセル}} [{{r|錆びた}}] {{y|<{{G|B}}{{C|D}}{{r|1}}>}}")]
    [TestCase(
        "{{y|ケムセル [{{r|rusted}}] <{{G|B}}{{C|D}}{{r|1}}>}}",
        "{{y|ケムセル [{{r|錆びた}}] <{{G|B}}{{C|D}}{{r|1}}>}}")]
    public void TranslatePreservingColors_PreservesSourceOwnedColorSpansInAngleCodeBase(
        string source,
        string expected)
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[rusted]", "[{{r|錆びた}}]"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(expected));
    }

    [Test]
    public void TranslatePreservingColors_DoesNotShiftColoredStainedModifierAcrossLocalizedAngleCodeBase()
    {
        UseProductionDictionaries();

        const string source = "{{r|blood}}-stained リストファン \u00040 \t0 [no cell] <CC13>";

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(InventoryLineTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(
                translated,
                Is.EqualTo("{{r|血}}に染まったリストファン \u00040 \t0 [セルなし] {{y|<{{B|C}}{{B|C}}{{r|1}}{{b|3}}>}}"));
            Assert.That(
                ColorShapeCaptureObservability.Capture(
                    nameof(InventoryLineTranslationPatch),
                    nameof(TranslatePreservingColors_DoesNotShiftColoredStainedModifierAcrossLocalizedAngleCodeBase),
                    source,
                    translated).MarkupSemanticStatus,
                Is.EqualTo("clean"));
        });
    }

    [Test]
    public void TranslatePreservingColors_DoesNotRemapLocalizedStainedAngleCodeBaseColorOntoStateSuffix()
    {
        UseProductionDictionaries();

        const string source = "{{r|{{r|blood}}-stained リストファン \u00040 \t0 [no cell]}} <CC13>";

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(InventoryLineTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(
                translated,
                Is.EqualTo("{{r|血}}に染まったリストファン \u00040 \t0 [セルなし] {{y|<{{B|C}}{{B|C}}{{r|1}}{{b|3}}>}}"));
            Assert.That(translated, Does.Not.Contain("{{r|{{r|血に染まっ}}"));
            Assert.That(translated, Does.Not.Contain("}}セ{{b|ル}}なし"));
        });
    }

    [Test]
    public void TranslatePreservingColors_PreservesColoredBitTagsInsideAngleCodeSuffix()
    {
        WriteDictionary(("worn bronze sword", "使い込まれた青銅の剣"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "worn bronze sword <{{R|A}}{{C|C}}>",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("使い込まれた青銅の剣 <{{R|A}}{{C|C}}>"));
    }

    [TestCase(
        "chem cell <{{|{{G|B}}{{C|D}}{{r|1}}}}>",
        "ケムセル <{{G|B}}{{C|D}}{{r|1}}>")]
    [TestCase(
        "{{y|[{{c|chem cell}} {{y|({{g|Fresh}})}} <{{|{{G|B}}{{C|D}}{{r|1}}}}>]}}",
        "{{y|[{{c|ケムセル}} {{y|({{g|残量多}})}} <{{G|B}}{{C|D}}{{r|1}}>]}}")]
    public void TranslatePreservingColors_RemovesEmptyQudWrapperFromRuntimeAngleCodeSuffix(
        string source,
        string expected)
    {
        WriteDictionary(("chem cell", "ケムセル"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(expected));
            Assert.That(
                ColorShapeCaptureObservability.Capture(
                    nameof(InventoryLineTranslationPatch),
                    nameof(TranslatePreservingColors_RemovesEmptyQudWrapperFromRuntimeAngleCodeSuffix),
                    source,
                    translated).MarkupSemanticStatus,
                Is.EqualTo("clean"));
        });
    }

    [TestCase(
        "Fresh",
        "{{y|[{{c|ケムセル}} {{y|(残量多)}} <{{G|B}}{{C|D}}{{r|1}}>]}}")]
    [TestCase(
        "SomeUnknownState",
        "{{y|[{{c|ケムセル}} {{y|(SomeUnknownState)}} <{{G|B}}{{C|D}}{{r|1}}>]}}")]
    public void TranslatePreservingColors_StripsDirectMarkerFromLoadedCellChargeStatus(string charge, string expected)
    {
        WriteDictionary(("chem cell", "ケムセル"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{y|[{{c|chem cell}} {{y|(" + MessageFrameTranslator.DirectTranslationMarker + charge
            + ")}} <{{|{{G|B}}{{C|D}}{{r|1}}}}>]}}",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(expected));
    }

    [Test]
    public void TranslatePreservingColors_PreservesLoadedCellColorsInsideBracketSuffix()
    {
        var source = "{{C|高級工具セット}} {{y|[{{c|ケムセル}} {{y|({{G|残量十分}})}} {{y|<{{G|B}}{{C|D}}{{r|1}}>}}]}}";

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(source));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesNestedLoadedCellLiquidAndStateInsideBracketSuffix()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("wrist calculator", "リスト計算機"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[auto-collecting]", "[自動収集中]"));
        WriteDictionaryFile(
            "ui-liquids.ja.json",
            ("oil", "油"),
            ("{{K|oil}}", "{{K|油}}"));

        const string source =
            "リストファン {{b|\u0004}}0 {{K|\t}}0 {{y|[{{K|燃焼}} {{c|セル}} {{y|[{{rules|8}} drams of {{K|oil}}]}} {{y|[{{c|auto-collecting}}]}} {{y|<{{G|B}}{{C|D}}{{g|2}}>}}]}} {{y|<{{B|C}}{{B|C}}{{r|1}}{{b|3}}>}}";

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "wrist calculator [燃焼 セル [8 drams of oil] [auto-collecting] <BD2>] <CC13>",
                    nameof(GetDisplayNamePatch)),
                Does.StartWith("リスト計算機 "));
            Assert.That(translated, Does.Contain("{{y|[{{rules|8}}ドラムの{{K|油}}]}}"));
            Assert.That(translated, Does.Contain("{{y|[{{c|自動収集中}}]}}"));
            Assert.That(translated, Does.Not.Contain("drams of"));
            Assert.That(translated, Does.Not.Contain("auto-collecting"));
        });
    }

    [Test]
    public void TranslatePreservingColors_DoesNotNestSourceLiquidColorAroundColoredQuantifiedLiquidTranslation()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("wrist calculator", "リスト計算機"));
        WriteDictionaryFile(
            "ui-liquids.ja.json",
            ("water", "{{B|水}}"));

        const string source =
            "リストファン {{b|\u0004}}0 {{K|\t}}0 {{y|[セル {{y|[{{rules|8}} drams of {{B|water}}]}} {{y|[auto-collecting]}} {{y|<{{G|B}}{{C|D}}{{g|2}}>}}]}} {{y|<{{B|C}}{{B|C}}{{r|1}}{{b|3}}>}}";

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.Contain("{{y|[{{rules|8}}ドラムの{{B|水}}]}}"));
            Assert.That(translated, Does.Not.Contain("{{B|{{B|水}}}}"));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesCaptureWrappedQuantifiedLiquidInsideNestedLoadedCell()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("wrist calculator", "リスト計算機"));
        WriteDictionaryFile(
            "ui-liquids.ja.json",
            ("blood", "血"));

        const string source =
            "リストファン {{b|\u0004}}0 {{K|\t}}0 {{y|[セル {{y|[{{rules|8}} drams of {{r|blood}}]}} {{y|[auto-collecting]}} {{y|<{{G|B}}{{C|D}}{{g|2}}>}}]}} {{y|<{{B|C}}{{B|C}}{{r|1}}{{b|3}}>}}";

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.Contain("{{y|[{{rules|8}}ドラムの{{r|血}}]}}"));
            Assert.That(translated, Does.Not.Contain("drams of"));
            Assert.That(translated, Does.Not.Contain("blood"));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesLoadedCellStateInsideBracketBeforeOuterAngleCode()
    {
        const string source = "濡れたリスト計算機 \u00040 \t0 [ケムセル (残量十分) <BD1>] <B124>";

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(LookTooltipInformationWrapPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.Contain("{{c|ケムセル}} {{y|({{G|残量十分}})}} {{y|<{{G|B}}{{C|D}}{{r|1}}>}}"));
            Assert.That(translated, Does.Contain("{{y|<{{G|B}}{{r|1}}{{g|2}}{{c|4}}>}}"));
            Assert.That(translated, Does.Not.Contain("[ケムセル (残量十分) <BD1>]"));
            Assert.That(translated, Does.Not.Contain("<B124>"));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesLeadingWhitespaceModifierChainBeforeWeaponStats()
    {
        WriteDictionary(("チェーンピストル", "チェーンピストル"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("masterwork", "傑作"),
            ("scoped", "スコープ付き"),
            ("[empty]", "[{{K|空}}]"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            " masterwork scoped チェーンピストル \u001a8 \u00031d6 [empty]",
            nameof(GetDisplayNamePatch));

        Assert.That(
            translated,
            Is.EqualTo(" 傑作 スコープ付き チェーンピストル {{c|\u001a}}8 {{r|\u0003}}1d6 [{{K|空}}]"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesModifierChainAfterLeadingZeroWidthMarkup()
    {
        WriteDictionary(("チェーンピストル", "チェーンピストル"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("masterwork", "傑作"),
            ("scoped", "スコープ付き"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{R|}} {{Y|masterwork}} scoped チェーンピストル",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{Y|傑作}} スコープ付き チェーンピストル"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesWeaponWithClauseAndKeepsExistingNameColor()
    {
        WriteDictionary(("laser rifle", "レーザーライフル"));
        WriteContextDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("beamsplitter", "GetDisplayName.Adjective", "{{R-R-r-r-g-g-G-G-B-B-b-b sequence|ビームスプリッタ装着}}"),
            ("[broken]", null, "[{{r|破損}}]"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{C|レーザー}}ライフル with {{R-R-r-r-g-g-G-G-B-B-b-b sequence|beamsplitter}} \u001a8 \u00031d12 [broken]",
            nameof(GetDisplayNamePatch));

        Assert.That(
            translated,
            Is.EqualTo("{{C|レーザー}}ライフル（{{R-R-r-r-g-g-G-G-B-B-b-b sequence|ビームスプリッタ装着}}） {{c|\u001a}}8 {{r|\u0003}}1d12 [{{r|破損}}]"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesWeaponWithClauseBeforeColoredStatsAndLoadedCell()
    {
        WriteContextDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("beamsplitter", "GetDisplayName.Adjective", "{{R-R-r-r-g-g-G-G-B-B-b-b sequence|ビームスプリッタ装着}}"));

        Assert.That(
            GetDisplayNameRouteTranslator.TranslatePreservingColors(
                "アイゲンライフル with beamsplitter",
                nameof(GetDisplayNamePatch)),
            Is.EqualTo("アイゲンライフル（{{R-R-r-r-g-g-G-G-B-B-b-b sequence|ビームスプリッタ装着}}）"));

        var source =
            "アイゲンライフル with {{R-R-r-r-g-g-G-G-B-B-b-b sequence|beamsplitter}} {{W|\u001a}}10 {{r|\u0003}}1d12 {{y|[{{w|フィジェット}} {{c|セル}} {{b|\u0004}}0 {{K|\t}}0 {{y|({{g|残量多}})}}]}}";
        var stripped = ColorCodePreserver.Strip(source).stripped;
        Assert.That(
            stripped,
            Is.EqualTo("アイゲンライフル with beamsplitter \u001a10 \u00031d12 [フィジェット セル \u00040 \t0 (残量多)]"));
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.That(
            translated,
            Is.EqualTo("アイゲンライフル（{{R-R-r-r-g-g-G-G-B-B-b-b sequence|ビームスプリッタ装着}}） {{W|\u001a}}10 {{r|\u0003}}1d12 {{y|[{{w|フィジェット}} {{c|セル}} {{b|\u0004}}0 {{K|\t}}0 {{y|({{g|残量多}})}}]}}"));
    }

    [Test]
    public void TranslatePreservingColors_PreservesLoadedCellColorsAfterPrefixModifierAndWeaponStats()
    {
        WriteDictionary(
            ("carbide battle axe", "カーバイドの戦斧"),
            ("chem cell", "ケムセル"));
        WriteContextDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("{{electrical|electrified}}", "GetDisplayName.Adjective", "{{electrical|帯電}}"));

        var source =
            "{{electrical|electrified}} {{b|carbide battle axe}} {{W|\u001a}}6 {{r|\u0003}}1d4+1 {{y|[{{c|chem cell}} {{y|({{g|Fresh}})}} <{{G|B}}{{C|D}}{{r|1}}>]}}";

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.That(
            translated,
            Is.EqualTo("{{electrical|帯電}} {{b|カーバイドの戦斧}} {{W|\u001a}}6 {{r|\u0003}}1d4+1 {{y|[{{c|ケムセル}} {{y|({{g|残量多}})}} <{{G|B}}{{C|D}}{{r|1}}>]}}"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesKnownDisplayNameWithClauses()
    {
        WriteDictionary(("laser rifle", "レーザーライフル"));
        WriteContextDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("beamsplitter", "GetDisplayName.Adjective", "ビームスプリッタ装着"),
            ("filters", "GetDisplayName.Adjective", "フィルター付き"),
            ("suspensors", "GetDisplayName.Adjective", "サスペンサー付き"),
            ("cleats", "GetDisplayName.Adjective", "スパイク付き"),
            ("piping", "GetDisplayName.Adjective", "配管"),
            ("electromagnetic shielding", "GetDisplayName.Adjective", "電磁シールド"),
            ("gearbox", "GetDisplayName.Adjective", "ギアボックス"),
            ("co-processor", "GetDisplayName.Adjective", "コプロセッサ"),
            ("quantum reverb", "GetDisplayName.Adjective", "量子リバーブ"),
            ("terrifying visage", "GetDisplayName.Adjective", "恐怖の顔貌"),
            ("serene visage", "GetDisplayName.Adjective", "静穏の顔貌"));

        Assert.Multiple(() =>
        {
            Assert.That(GetDisplayNameRouteTranslator.TranslatePreservingColors("laser rifle with beamsplitter", nameof(GetDisplayNamePatch)), Is.EqualTo("レーザーライフル（ビームスプリッタ装着）"));
            Assert.That(GetDisplayNameRouteTranslator.TranslatePreservingColors("laser rifle with filters", nameof(GetDisplayNamePatch)), Is.EqualTo("レーザーライフル（フィルター付き）"));
            Assert.That(GetDisplayNameRouteTranslator.TranslatePreservingColors("laser rifle with suspensors", nameof(GetDisplayNamePatch)), Is.EqualTo("レーザーライフル（サスペンサー付き）"));
            Assert.That(GetDisplayNameRouteTranslator.TranslatePreservingColors("laser rifle with cleats", nameof(GetDisplayNamePatch)), Is.EqualTo("レーザーライフル（スパイク付き）"));
            Assert.That(GetDisplayNameRouteTranslator.TranslatePreservingColors("laser rifle with piping", nameof(GetDisplayNamePatch)), Is.EqualTo("レーザーライフル（配管）"));
            Assert.That(GetDisplayNameRouteTranslator.TranslatePreservingColors("laser rifle with electromagnetic shielding", nameof(GetDisplayNamePatch)), Is.EqualTo("レーザーライフル（電磁シールド）"));
            Assert.That(GetDisplayNameRouteTranslator.TranslatePreservingColors("laser rifle with gearbox", nameof(GetDisplayNamePatch)), Is.EqualTo("レーザーライフル（ギアボックス）"));
            Assert.That(GetDisplayNameRouteTranslator.TranslatePreservingColors("laser rifle with co-processor", nameof(GetDisplayNamePatch)), Is.EqualTo("レーザーライフル（コプロセッサ）"));
            Assert.That(GetDisplayNameRouteTranslator.TranslatePreservingColors("laser rifle with quantum reverb", nameof(GetDisplayNamePatch)), Is.EqualTo("レーザーライフル（量子リバーブ）"));
            Assert.That(GetDisplayNameRouteTranslator.TranslatePreservingColors("laser rifle with terrifying visage", nameof(GetDisplayNamePatch)), Is.EqualTo("レーザーライフル（恐怖の顔貌）"));
            Assert.That(GetDisplayNameRouteTranslator.TranslatePreservingColors("laser rifle with serene visage", nameof(GetDisplayNamePatch)), Is.EqualTo("レーザーライフル（静穏の顔貌）"));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesAllKnownGameWithClauseModifierPhrases()
    {
        UseProductionDictionaries();

        Assert.Multiple(() =>
        {
            foreach (var testCase in KnownGameWithClauseModifierPhrases())
            {
                var source = "レーザーライフル with " + testCase.Source + " \u001a8 \u00031d12 [empty]";
                var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(source, nameof(GetDisplayNamePatch));

                Assert.That(translated, Does.Contain("（" + testCase.Expected + "）"), testCase.Id);
                Assert.That(translated, Does.EndWith(" {{c|\u001a}}8 {{r|\u0003}}1d12 [空]"), testCase.Id);
                Assert.That(
                    ColorShapeCaptureObservability.Capture(
                        nameof(InventoryLineTranslationPatch),
                        testCase.Id,
                        source,
                        translated).MarkupSemanticStatus,
                    Is.EqualTo("clean"),
                    testCase.Id);
            }
        });
    }

    [Test]
    public void TranslatePreservingColors_PreservesSourceColorOnPlainWithClauseTranslation()
    {
        WriteDictionary(("laser rifle", "レーザーライフル"));
        WriteContextDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("filters", "GetDisplayName.Adjective", "フィルター付き"),
            ("[empty]", null, "[空]"));

        Assert.That(
            GetDisplayNameRouteTranslator.TranslatePreservingColors(
                "laser rifle with {{Y|filters}}",
                nameof(GetDisplayNamePatch)),
            Is.EqualTo("レーザーライフル（{{Y|フィルター付き}}）"));

        Assert.That(
            GetDisplayNameRouteTranslator.TranslatePreservingColors(
                "laser rifle with {{Y|filters}} {{W|\u001a}}8 {{r|\u0003}}1d12 [empty]",
                nameof(GetDisplayNamePatch)),
            Is.EqualTo("レーザーライフル（{{Y|フィルター付き}}） {{W|\u001a}}8 {{r|\u0003}}1d12 [空]"));

        Assert.That(
            GetDisplayNameRouteTranslator.TranslatePreservingColors(
                "{{C|laser rifle}} with {{Y|filters}} {{W|\u001a}}8 {{r|\u0003}}1d12 [empty]",
                nameof(GetDisplayNamePatch)),
            Is.EqualTo("{{C|レーザーライフル}}（{{Y|フィルター付き}}） {{W|\u001a}}8 {{r|\u0003}}1d12 [空]"));

        Assert.That(
            GetDisplayNameRouteTranslator.TranslatePreservingColors(
                "{{W|laser rifle with filters}}",
                nameof(GetDisplayNamePatch)),
            Is.EqualTo("{{W|レーザーライフル（フィルター付き）}}"));

        Assert.That(
            GetDisplayNameRouteTranslator.TranslatePreservingColors(
                "laser rifle with {{Y|filters}} \u001a8 \u00031d12 [empty]",
                nameof(GetDisplayNamePatch)),
            Is.EqualTo("レーザーライフル（{{Y|フィルター付き}}） {{c|\u001a}}8 {{r|\u0003}}1d12 [空]"));

        Assert.That(
            GetDisplayNameRouteTranslator.TranslatePreservingColors(
                "laser rifle with {{Y|filters}} \u001a8 \u00031d12 <AD14>",
                nameof(GetDisplayNamePatch)),
            Is.EqualTo("レーザーライフル（{{Y|フィルター付き}}） {{c|\u001a}}8 {{r|\u0003}}1d12 {{y|<{{B|A}}{{B|D}}{{g|1}}{{g|4}}>}}"));

        Assert.That(
            GetDisplayNameRouteTranslator.TranslatePreservingColors(
                "laser rifle with {{Y|filters}} {{W|\u001a}}8 {{r|\u0003}}1d12 {{y|<{{B|A}}{{B|D}}{{g|1}}{{g|4}}>}}",
                nameof(GetDisplayNamePatch)),
            Is.EqualTo("レーザーライフル（{{Y|フィルター付き}}） {{W|\u001a}}8 {{r|\u0003}}1d12 {{y|<{{B|A}}{{B|D}}{{g|1}}{{g|4}}>}}"));
    }

    [Test]
    public void TranslatePreservingColors_FallsBackFromSourceOwnedWithClauseReaderForPlainTails()
    {
        WriteDictionary(("laser rifle", "レーザーライフル"));
        WriteContextDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("filters", "GetDisplayName.Adjective", "フィルター付き"),
            ("[empty]", null, "[空]"));

        Assert.Multiple(() =>
        {
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "laser rifle with filters {{W|\u001a}}8 {{r|\u0003}}1d12 [empty]",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("レーザーライフル（フィルター付き） {{W|\u001a}}8 {{r|\u0003}}1d12 [空]"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "laser rifle with filters [empty]",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("レーザーライフル（フィルター付き） [空]"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "laser rifle with ",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("laser rifle with "));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "laser rifle with filters {later}",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("laser rifle with filters {later}"));
        });
    }

    [Test]
    public void TranslatePreservingColors_UsesScopedStateTemplateLookup()
    {
        WriteDictionary(
            ("dromad merchant", "ドロマド商人"),
            ("chair", "椅子"));
        WriteDictionaryFile(
            "Scoped/ui-displayname-state-templates.ja.json",
            "{\"entries\":[{\"key\":\"sitting on {0}\",\"context\":\"GetDisplayName.StateTemplate\",\"text\":\"{0}に座っている\"}]}\n");

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "dromad merchant [sitting on a chair]",
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("ドロマド商人 [椅子に座っている]"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("sitting on {0}"), Is.EqualTo(0));
        });
    }

    [TestCase("花瓶 [空]")]
    [TestCase("タム、ドロマド商人 [座っている]")]
    public void TranslatePreservingColors_PassesThroughAlreadyLocalizedBracketedDisplayName(string source)
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(source));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_UsesLowerAsciiFallbackForParenthesizedState()
    {
        WriteDictionary(
            ("lead slug", "鉛の弾"),
            ("frozen", "凍結"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "lead slug (Frozen)",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("鉛の弾 (凍結)"));
    }

    [Test]
    public void TranslatePreservingColors_UsesExactLookupForWholeDisplayName()
    {
        WriteDictionary(("worn bronze sword", "使い込まれた青銅の剣"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "worn bronze sword",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("使い込まれた青銅の剣"));
    }

    [Test]
    public void TranslatePreservingColors_UsesTrimmedExactLookupForWholeDisplayName()
    {
        WriteDictionary(("worn bronze sword", "使い込まれた青銅の剣"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "  worn bronze sword  ",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("  使い込まれた青銅の剣  "));
    }

    [Test]
    public void TranslatePreservingColors_PrefersDisplayNameScopedDictionaryForConflictingLiquidKey()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("water", "{{B|水の}}"));
        WriteDictionaryFile(
            "ui-liquids.ja.json",
            ("water", "水"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "water",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{B|水の}}"));
    }

    [Test]
    public void TranslatePreservingColors_PrefersExactWholeDisplayNameLookupBeforeProperNameModifierHeuristic()
    {
        WriteDictionary(("Water Containers", "水容器"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("water", "{{B|水の}}"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "Water Containers",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("水容器"));
    }

    [Test]
    public void TranslatePreservingColors_PrefersAtomicDisplayNameBeforeProperNameModifierHeuristic()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("Lead Slug", "鉛スラッグ"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("lead", "鉛"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "Lead Slug",
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("鉛スラッグ"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Lead Slug"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_DoesNotTranslateProperNameHeadFromGlobalDictionary()
    {
        WriteDictionary(("Point", "地点"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "Point of the Commanding Woe",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("Point of the Commanding Woe"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesObservedAtomicDisplayName()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("Wooden Arrow", "木の矢"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "Wooden Arrow",
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("木の矢"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("Wooden Arrow"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_UsesShippedGeneratedCanvasTentComponents()
    {
        var repositoryDictionaryPath = Path.GetFullPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../Localization/Dictionaries"));
        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(repositoryDictionaryPath);

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "dragonfly chitin tent",
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("トンボのキチン質の天幕"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("dragonfly chitin tent"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_CachesEmptyLocalizedBlueprintMarkup_WhenObjectBlueprintsDirectoryIsMissing()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(tempDirectory);
        WriteDictionaryFile("ui-displayname-adjectives.ja.json", ("[empty]", "[空]"));

        Assert.That(
            GetDisplayNameRouteTranslator.TranslatePreservingColors("塩ホッパー [empty]", nameof(GetDisplayNamePatch)),
            Is.EqualTo("塩ホッパー [空]"));

        var objectBlueprintsDirectory = Path.Combine(tempDirectory, "ObjectBlueprints");
        Directory.CreateDirectory(objectBlueprintsDirectory);
        File.WriteAllText(
            Path.Combine(objectBlueprintsDirectory, "Items.jp.xml"),
            "<objects><object><part DisplayName=\"{{Y|塩ホッパー}}\" /></object></objects>");

        Assert.That(
            GetDisplayNameRouteTranslator.TranslatePreservingColors("塩ホッパー [empty]", nameof(GetDisplayNamePatch)),
            Is.EqualTo("塩ホッパー [空]"));
    }

    [Test]
    public void TranslatePreservingColors_CachesEmptyLocalizedBlueprintMarkup_WhenXmlParseFails()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(tempDirectory);
        WriteDictionaryFile("ui-displayname-adjectives.ja.json", ("[empty]", "[空]"));
        var objectBlueprintsDirectory = Path.Combine(tempDirectory, "ObjectBlueprints");
        Directory.CreateDirectory(objectBlueprintsDirectory);
        var filePath = Path.Combine(objectBlueprintsDirectory, "Items.jp.xml");
        File.WriteAllText(filePath, "<objects><object><part DisplayName=\"{{Y|塩ホッパー}}\"");

        Assert.That(
            GetDisplayNameRouteTranslator.TranslatePreservingColors("塩ホッパー [empty]", nameof(GetDisplayNamePatch)),
            Is.EqualTo("塩ホッパー [空]"));

        File.WriteAllText(
            filePath,
            "<objects><object><part DisplayName=\"{{Y|塩ホッパー}}\" /></object></objects>");

        Assert.That(
            GetDisplayNameRouteTranslator.TranslatePreservingColors("塩ホッパー [empty]", nameof(GetDisplayNamePatch)),
            Is.EqualTo("塩ホッパー [空]"));
    }

    [Test]
    public void TranslatePreservingColors_UsesReadableLocalizedBlueprintMarkup_WhenAnotherXmlFileFails()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(tempDirectory);
        WriteDictionaryFile("ui-displayname-adjectives.ja.json", ("[empty]", "[空]"));
        var objectBlueprintsDirectory = Path.Combine(tempDirectory, "ObjectBlueprints");
        Directory.CreateDirectory(objectBlueprintsDirectory);
        File.WriteAllText(
            Path.Combine(objectBlueprintsDirectory, "Broken.jp.xml"),
            "<objects><object><part DisplayName=\"{{Y|壊れた}}\"");
        File.WriteAllText(
            Path.Combine(objectBlueprintsDirectory, "Items.jp.xml"),
            "<objects><object><part DisplayName=\"{{Y|塩ホッパー}}\" /></object></objects>");

        Assert.That(
            GetDisplayNameRouteTranslator.TranslatePreservingColors("塩ホッパー [empty]", nameof(GetDisplayNamePatch)),
            Is.EqualTo("{{Y|塩ホッパー}} [空]"));
    }

    [Test]
    public void TranslatePreservingColors_DoesNotRestoreLocalizedBlueprintMarkup_WhenVisibleNameIsAmbiguous()
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(tempDirectory);
        WriteDictionaryFile("ui-displayname-adjectives.ja.json", ("[empty]", "[空]"));
        var objectBlueprintsDirectory = Path.Combine(tempDirectory, "ObjectBlueprints");
        Directory.CreateDirectory(objectBlueprintsDirectory);
        File.WriteAllText(
            Path.Combine(objectBlueprintsDirectory, "A.jp.xml"),
            "<objects><object><part DisplayName=\"{{Y|塩ホッパー}}\" /></object></objects>");
        File.WriteAllText(
            Path.Combine(objectBlueprintsDirectory, "B.jp.xml"),
            "<objects><object><part DisplayName=\"{{R|塩ホッパー}}\" /></object></objects>");

        Assert.That(
            GetDisplayNameRouteTranslator.TranslatePreservingColors("塩ホッパー [empty]", nameof(GetDisplayNamePatch)),
            Is.EqualTo("塩ホッパー [空]"));
    }

    [TestCase("blank mural slate", "空白の壁画石板")]
    [TestCase("ruined mural slate", "崩れた壁画石板")]
    public void TranslatePreservingColors_UsesShippedMuralSlateLeaves(string source, string expected)
    {
        UseProductionDictionaries();

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo(expected));
            Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
        });
    }

    [TestCase("advertisement for {{M|クユラミルの蒸留所, 伝説の樹液商}}", "{{M|クユラミルの蒸留所, 伝説の樹液商}}の広告")]
    [TestCase("advertisement for {{M|Resheph}}", "{{M|レシェフ}}の広告")]
    [TestCase("advertisement for \u0001{{M|レシェフ}}", "{{M|レシェフ}}の広告")]
    [TestCase("advertisement for {{M|unknown merchant}}", "{{M|unknown merchant}}の広告")]
    [TestCase("advertisement for {{M|Resheph}} [empty]", "{{M|レシェフ}}の広告 [空]")]
    [TestCase("clone of a snapjaw", "スナップジョーのクローン")]
    [TestCase("hologram of a snapjaw", "スナップジョーのホログラム")]
    [TestCase("phylactery of High Templar", "高位聖堂騎士のファイラクテリー")]
    [TestCase("mural of Resheph", "レシェフの壁画")]
    [TestCase("ruined mural of Resheph", "レシェフの崩れた壁画")]
    [TestCase("shrine to Resheph", "レシェフの祠")]
    [TestCase("villagers of Joppa", "ジョッパの村人")]
    [TestCase("Cult of Baram", "バラム教団")]
    public void TranslatePreservingColors_TranslatesGeneratedEnglishPrefixDisplayNames(
        string source,
        string expected)
    {
        WriteDictionary(
            ("snapjaw", "スナップジョー"),
            ("High Templar", "高位聖堂騎士"),
            ("Resheph", "レシェフ"),
            ("Joppa", "ジョッパ"),
            ("Baram", "バラム"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[empty]", "[空]"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(expected));
    }

    [Test]
    public void TranslatePreservingColors_TreatsRuntimeEmptyQudAngleCodeWrapperAsSemanticClean()
    {
        const string source =
            "phylactery of {{M|ベグナスパルド・ベマリネ}} {{y|<{{|{{G|B}}{{B|C}}{{c|4}}{{W|6}}}}>}}";

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNameProcessPatch));

        Assert.Multiple(() =>
        {
            Assert.That(
                translated,
                Is.EqualTo("{{M|ベグナスパルド・ベマリネ}} {{y|<{{|{{G|B}}{{B|C}}{{c|4}}{{W|6}}}}>}}のファイラクテリー"));
            Assert.That(
                ColorShapeCaptureObservability.Capture(
                    nameof(GetDisplayNameProcessPatch),
                    nameof(TranslatePreservingColors_TreatsRuntimeEmptyQudAngleCodeWrapperAsSemanticClean),
                    source,
                    translated).MarkupSemanticStatus,
                Is.EqualTo("clean"));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesRuntimeObservedShrineCognomenTarget()
    {
        WriteDictionaryFile(
            "Scoped/historyspice-common.ja.json",
            ("potent", "強大な"),
            ("ghost", "幽鬼"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "shrine to ウーヒム II, the Potent Ghost",
            nameof(GetDisplayNameProcessPatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("ウーヒム II、強大な幽鬼の祠"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("the Potent Ghost"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesBaetylRelicNameWhilePreservingColorAndSuffix()
    {
        WriteDictionaryFile(
            "Scoped/historyspice-common.ja.json",
            ("analog", "アナログの"));
        const string source = "{{M|Chain of the Analog Sand}} \u00040 \t0 [6ドラムのゲル]";

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            "InventoryActionMenu.Title");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("{{M|アナログの砂の鎖}} \u00040 \t0 [6ドラムのゲル]"));
            Assert.That(
                ColorShapeCaptureObservability.Capture(
                    nameof(GetDisplayNameRouteTranslator),
                    nameof(TranslatePreservingColors_TranslatesBaetylRelicNameWhilePreservingColorAndSuffix),
                    source,
                    translated).MarkupSemanticStatus,
                Is.EqualTo("clean"));
        });
    }

    [TestCase(
        "{{Y|Skillsoft [{{W|Tinkering}}]}}",
        "{{Y|スキルソフト [{{W|工作}}]}}")]
    [TestCase(
        "{{Y|Skillsoft Plus [{{W|Tactics}}]}}",
        "{{Y|スキルソフト・プラス [{{W|戦術}}]}}")]
    [TestCase(
        "{{Y|Skillsoft [{{W|Long Blade}}]}}",
        "{{Y|スキルソフト [{{W|Long Blade}}]}}")]
    [TestCase(
        "{{Y|Skillsoft [{{W|\u0001工作}}]}}",
        "{{Y|スキルソフト [{{W|工作}}]}}")]
    public void TranslatePreservingColors_TranslatesCyberneticsSkillsoftGeneratedDisplayNames(
        string source,
        string expected)
    {
        WriteDictionary(("Tinkering", "工作"), ("Tactics", "戦術"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(expected));
    }

    [TestCase("", "")]
    [TestCase("\u0001Skillsoft [{{W|Tinkering}}]", "\u0001Skillsoft [{{W|Tinkering}}]")]
    public void TranslatePreservingColors_SkillsoftGeneratedDisplayNameEdgeInputsPassThrough(
        string source,
        string expected)
    {
        WriteDictionary(("Tinkering", "工作"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(expected));
    }

    [TestCase("defoliant grenade mk I miner mk I", "落葉剤グレネード mk I 採掘機 mk I")]
    [TestCase("defoliant grenade mk I bomber mk II", "落葉剤グレネード mk I 爆撃機 mk II")]
    [TestCase("<color=green>defoliant grenade mk I miner mk I</color>", "<color=green>落葉剤グレネード mk I 採掘機 mk I</color>")]
    [TestCase("{{C|defoliant grenade mk I bomber mk II}}", "{{C|落葉剤グレネード mk I 爆撃機 mk II}}")]
    [TestCase("{{C|odd grenade mk I miner mk I}}", "{{C|odd grenade mk I 採掘機 mk I}}")]
    public void TranslatePreservingColors_TranslatesMinerGeneratedRoleSuffix(
        string source,
        string expected)
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("defoliant grenade", "落葉剤グレネード"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNameProcessPatch));

        Assert.That(translated, Is.EqualTo(expected));
    }

    [TestCase("", "")]
    [TestCase("\u0001defoliant grenade mk I miner mk I", "\u0001defoliant grenade mk I miner mk I")]
    public void TranslatePreservingColors_MinerGeneratedRoleSuffixNonTranslatableEdgeInputsPassThrough(
        string source,
        string expected)
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNameProcessPatch));

        Assert.That(translated, Is.EqualTo(expected));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesMinerGeneratedRoleSuffixWhenBaseUnknown()
    {
        var source = "odd grenade mk I miner mk I";

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNameProcessPatch));

        Assert.That(translated, Is.EqualTo("odd grenade mk I 採掘機 mk I"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesColoredMinerGeneratedRoleSuffixWhenBaseUnknown()
    {
        var source = "<color=green>odd grenade mk I miner mk I</color>";

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNameProcessPatch));

        Assert.That(translated, Is.EqualTo("<color=green>odd grenade mk I 採掘機 mk I</color>"));
    }

    [TestCase(
        "{{K|amaranthine}} prism",
        "{{K|アマランス色}}のプリズム")]
    [TestCase(
        "{{K|amara{{y|n}}thine}} prism",
        "{{K|アマラ}}{{y|ン}}{{K|ス色}}のプリズム")]
    [TestCase(
        "{{K|amar{{y|a{{Y|n}}t}}hine}} prism",
        "{{K|アマラ}}{{y|ン}}{{Y|ス}}{{K|色}}のプリズム")]
    [TestCase(
        "{{K|am{{y|ar{{Y|a{{R|n}}t}}hi}}ne}} prism",
        "{{K|アマ}}{{y|ラ}}{{Y|ン}}{{R|ス}}{{K|色}}のプリズム")]
    [TestCase(
        "{{y|am{{Y|a{{y|r{{r|a{{R|n}}t}}h}}i}}ne}} prism",
        "{{y|アマ}}{{Y|ラ}}{{y|ン}}{{r|ス}}{{R|色}}のプリズム")]
    [TestCase(
        "{{r|a{{R|m{{Y|a{{y|r{{r|a{{R|n}}t}}h}}i}}n}}e}} prism",
        "{{r|ア}}{{R|マ}}{{Y|ラ}}{{y|ン}}{{r|ス}}{{R|色}}のプリズム")]
    [TestCase(
        "{{Y|Schemasoft [{{C|Pistols, Mid Tier}}]}}",
        "{{Y|スキーマソフト [{{C|ピストル, 中位}}]}}")]
    [TestCase(
        "Schemasoft [Ammo and Energy Cells, Low Tier]",
        "スキーマソフト [弾薬とエネルギーセル, 下位]")]
    [TestCase(
        "{{Y|Schemasoft [{{C|Heavy Weapons, High Tier}}]}}",
        "{{Y|スキーマソフト [{{C|重火器, 上位}}]}}")]
    public void TranslatePreservingColors_TranslatesShippedGeneratedDisplayNames(
        string source,
        string expected)
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(expected));
    }

    [TestCase("open air", "開けた空間")]
    [TestCase("craggy ledge", "険しい岩棚")]
    public void TranslatePreservingColors_TranslatesPitMaterialRuntimeDisplayNames(
        string source,
        string expected)
    {
        WriteDictionary(
            ("open air", "開けた空間"),
            ("craggy ledge", "険しい岩棚"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(expected));
    }

    [TestCase("Evil snapjaw", "邪悪なスナップジョー")]
    [TestCase("Refracted {{Y|snapjaw}}", "屈折した{{Y|スナップジョー}}")]
    [TestCase("anti-snapjaw", "反スナップジョー")]
    [TestCase("anti-{{Y|スナップジョー}}", "反スナップジョー")]
    public void TranslatePreservingColors_TranslatesEvilTwinGeneratedDisplayNames(
        string source,
        string expected)
    {
        WriteDictionary(("snapjaw", "スナップジョー"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(expected));
    }

    [TestCase("Schemasoft [Unknown, Low Tier]")]
    [TestCase("{{Y|Schemasoft [{{C|Pistols, Unknown Tier}}]}}")]
    public void TranslatePreservingColors_LeavesUnknownCyberneticsSchemasoftGeneratedDisplayNameUnchanged(
        string source)
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(source));
    }

    [Test]
    public void TranslatePreservingColors_PrefersTrimmedExactLookupBeforeProperNameModifierHeuristic()
    {
        WriteDictionary(("Water Containers", "水容器"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("water", "{{B|水の}}"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "Water Containers  ",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("水容器  "));
    }

    [Test]
    public void TranslatePreservingColors_PrefersDisplayNameScopedDictionaryForConflictingLiquidAdjectiveKey()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("bloody", "{{r|血まみれの}}"));
        WriteDictionaryFile(
            "ui-liquid-adjectives.ja.json",
            ("bloody", "血混じりの"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "bloody Naruur",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{r|血まみれの}}Naruur"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesLiquidCooledAdjectiveInsideLiquidColorMarkup()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("liquid-cooled", "液冷式"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{B|liquid-cooled}}",
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("{{B|液冷式}}"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("{{B|liquid-cooled}}"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_PreservesMarkupWrappedEnglishModifierWithoutNestedColorCorruption()
    {
        WriteDictionary(("dromad merchant", "ドロマド商人"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("bloody", "{{r|血まみれの}}"),
            ("[sitting]", "[座っている]"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{r|bloody}} Tam, dromad merchant [sitting]",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{r|血まみれの}}Tam、ドロマド商人 [座っている]"));
    }

    [Test]
    public void TranslatePreservingColors_DoesNotReapplySourceModifierMarkup_WhenTranslatedModifierOwnsMarkup()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("wet", "{{B|濡れた}}"),
            ("[swimming]", "[泳いでいる]"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{B|wet グロウフィッシュ}} [swimming]",
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("{{B|濡れた}}グロウフィッシュ [泳いでいる]"));
            Assert.That(translated, Does.Not.Contain("{{B|{{B|"));
            Assert.That(translated, Does.Not.Contain("{{B}}|"));
        });
    }

    [Test]
    public void TranslatePreservingColors_DoesNotReapplySourceStateMarkup_WhenTranslatedStateOwnsMarkup()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[empty]", "[{{K|空}}]"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "水袋 {{y|[empty]}}",
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("水袋 {{y|[{{K|空}}]}}"));
            Assert.That(translated, Does.Not.Contain("[{{K|空]}}"));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesColorWrappedStaticDisplayNameStates()
    {
        WriteDictionary(
            ("iron sword", "鉄の剣"),
            ("snapjaw", "スナップジョー"),
            ("banner", "旗"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[rusted]", "[{{r|錆びた}}]"),
            ("[broken]", "[{{r|破損}}]"),
            ("[cracked]", "[{{r|ひび割れ}}]"),
            ("[wading]", "[{{B|浅瀬を進んでいる}}]"),
            ("[raised]", "[{{g|掲揚中}}]"));

        Assert.Multiple(() =>
        {
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "iron sword [{{r|rusted}}]",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("鉄の剣 [{{r|錆びた}}]"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "iron sword [{{r|broken}}]",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("鉄の剣 [{{r|破損}}]"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "iron sword [{{r|cracked}}]",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("鉄の剣 [{{r|ひび割れ}}]"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "snapjaw {{y|[{{B|wading}}]}}",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("スナップジョー {{y|[{{B|浅瀬を進んでいる}}]}}"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "banner {{y|[{{g|raised}}]}}",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("旗 {{y|[{{g|掲揚中}}]}}"));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesStaticDisplayNameAdjectives()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("magnetized", "磁化した"),
            ("fungus-ridden", "真菌まみれの"),
            ("grenade", "グレネード"),
            ("snapjaw", "スナップジョー"));

        Assert.Multiple(() =>
        {
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "magnetized 鉄の剣",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("磁化した鉄の剣"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "fungus-ridden スナップジョー",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("真菌まみれのスナップジョー"));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesDynamicBracketedDisplayNameStates()
    {
        WriteDictionary(
            ("mine", "地雷"),
            ("ingredient", "食材"),
            ("rack", "ラック"),
            ("deed", "証書"),
            ("magazine", "マガジン"),
            ("Hindren", "ヒンドレン"),
            ("lead slug", "鉛スラッグ"),
            ("snapjaw", "スナップジョー"),
            ("iron sword", "鉄の剣"),
            ("web", "網"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("stuck", "拘束"));
        WriteDictionaryFile(
            "Scoped/ui-displayname-state-templates.ja.json",
            "{\"entries\":["
            + "{\"key\":\"stuck in {0}\",\"context\":\"GetDisplayName.StateTemplate\",\"text\":\"{0}にはまっている\"},"
            + "{\"key\":\"grabbed by {0}\",\"context\":\"GetDisplayName.StateTemplate\",\"text\":\"{0}につかまれている\"}"
            + "]}\n");

        Assert.Multiple(() =>
        {
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "mine {{y|[{{R|10 sec}}]}}",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("地雷 {{y|[{{R|10秒}}]}}"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "ingredient {{y|[{{C|3}} cooking servings]}}",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("食材 {{y|[調理3回分]}}"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "rack {{y|[2 cells]}}",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("ラック {{y|[セル2個]}}"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "deed {{y|[Hindren chapter]}}",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("証書 {{y|[ヒンドレン支部]}}"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "magazine {{y|[lead slug]}}",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("マガジン {{y|[鉛スラッグ]}}"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "snapjaw [{{B|stuck in a web}}]",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("スナップジョー [{{B|網にはまっている}}]"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "snapjaw [stuck in a 凍結した 塩分混じりの粘液の水たまり]",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("スナップジョー [凍結した 塩分混じりの粘液の水たまりにはまっている]"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "snapjaw [{{B|grabbed by an iron sword}}]",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("スナップジョー [{{B|鉄の剣につかまれている}}]"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "snapjaw [wrapped around a web]",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("スナップジョー [wrapped around a web]"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "\u0001snapjaw [stuck in a web]",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("\u0001snapjaw [stuck in a web]"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    string.Empty,
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo(string.Empty));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "{{B|}}",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("{{B|}}"));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesLocalizedPrefixWithAsciiTailStructurally()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("neutronic", "{{neutronic|中性子質の}}"),
            ("oozing", "{{K|滲み出ている}}"),
            ("spiced", "香辛料入りの"),
            ("tetrasludge", "テトラスラッジ"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "伝説の芳醇な neutronic oozing spiced tetrasludge",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("伝説の芳醇な{{neutronic|中性子質の}}{{K|滲み出ている}}香辛料入りのテトラスラッジ"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesMarkupOnlyAdjective()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("jewel-encrusted", "宝石をちりばめた"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{m-G-R-W-c-y-B-m-r-W-r-W-c-R-b sequence|jewel-encrusted}}",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{m-G-R-W-c-y-B-m-r-W-r-W-c-R-b sequence|宝石をちりばめた}}"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesGeneratedBookEditionSuffix()
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{W|Codex of Leaves, 2nd Edition}}",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{W|Codex of Leaves、第2版}}"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesRuntimeObservedSingleTitleSuffix()
    {
        WriteContextDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("hired guard", "GetDisplayName.Title", "雇われ護衛"));

        Assert.Multiple(() =>
        {
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "{{W|スパークティック}} and hired guard",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("{{W|スパークティック}}、雇われ護衛"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "スナップジョーの餌係 and hired guard",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("スナップジョーの餌係、雇われ護衛"));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesRuntimeObservedTitleSuffixWithState()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("Mechanimist convert", "メカニミスト改宗者"),
            ("Oboroqoru", "オボロコル"));

        Assert.Multiple(() =>
        {
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "ドロマドの行商人 and Mechanimist convert [{{B|座っている}}]",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("ドロマドの行商人、メカニミスト改宗者 [{{B|座っている}}]"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "{{W|ドロマドの行商人 and Mechanimist convert [{{B|座っている}}]}}",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("{{W|ドロマドの行商人、メカニミスト改宗者 [{{B|座っている}}]}}"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "スクラップ・ショベラー and worshipper of Oboroqoru [{{B|座っている}}]",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("スクラップ・ショベラー、オボロコルの崇拝者 [{{B|座っている}}]"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "山羊人のシャーマンand worshipper of Oboroqoru [{{B|座っている}}]",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("山羊人のシャーマン、オボロコルの崇拝者 [{{B|座っている}}]"));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesRuntimeObservedSocialRoleTitleSuffixes()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("fungi", "菌類"));

        Assert.Multiple(() =>
        {
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "ヒヒand friend to fungi",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("ヒヒ、菌類の友"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "種吐きの蔓 and pariah to their people [座っている]",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("種吐きの蔓、同胞からの追放者 [座っている]"));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesImplantedMarkupAdjective()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("implanted", "{{implanted|埋め込み済み}}"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{implanted|implanted}} サイバネティック主体",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{implanted|埋め込み済み}} サイバネティック主体"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesSlimyAdjectiveWithoutNumeNumeWording()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("{{slimy|slimy}}", "{{slimy|粘液質の}}"),
            ("slime", "{{g|スライム}}"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{slimy|slimy}} slime",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{slimy|粘液質の}} {{g|スライム}}"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesSizeAdjectiveBeforeLocalizedBase()
    {
        WriteContextDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("small", "GetDisplayName.Adjective", "小さな"),
            ("large", "GetDisplayName.Adjective", "大きな"),
            ("{{B|wet}}", "GetDisplayName.Adjective", "{{B|濡れた}}"),
            ("{{slimy|slimy}}", "GetDisplayName.Adjective", "{{slimy|粘液質の}}"));

        Assert.Multiple(() =>
        {
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "small 岩塊",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("小さな岩塊"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "{{slimy|slimy}} {{B|wet}} small 岩塊",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("{{slimy|粘液質の}} {{B|濡れた}} 小さな岩塊"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "粘液質の濡れたsmall 岩塊",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("粘液質の濡れた小さな岩塊"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "血まみれのlarge 岩塊",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("血まみれの大きな岩塊"));
        });
    }

    [Test]
    public void TranslatePreservingColors_StripsLowercaseArticleModifierBeforeDisplayName()
    {
        WriteContextDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("small", "GetDisplayName.Adjective", "小さな"));

        Assert.Multiple(() =>
        {
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "the small 岩塊",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("小さな岩塊"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "{{K|the}} 岩塊",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("岩塊"));
        });
    }

    [Test]
    public void TranslatePreservingColors_UsesDisplayNameAdjectiveContextForMarkupWeaponModifiers()
    {
        WriteContextDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("{{Y|masterwork}}", "GetDisplayName.Adjective", "{{Y|接頭辞masterwork}}"),
            ("{{electrical|electrified}}", "GetDisplayName.Adjective", "{{electrical|接頭辞electrified}}"),
            ("{{scaled|scaled}}", "GetDisplayName.Adjective", "{{scaled|接頭辞scaled}}"),
            ("counterweighted", "GetDisplayName.Adjective", "接頭辞counterweighted"),
            ("masterwork", "GetDisplayName.Adjective", "接頭辞masterwork"),
            ("scoped", "GetDisplayName.Adjective", "接頭辞scoped"),
            ("electrified", "GetDisplayName.Adjective", "接頭辞electrified"),
            ("steel long sword", null, "鋼のロングソード"));
        WriteDictionaryFile(
            "world-mods.ja.json",
            ("{{Y|masterwork}}", "{{Y|説明masterwork}}"),
            ("{{electrical|electrified}}", "{{electrical|説明electrified}}"),
            ("{{scaled|scaled}}", "{{scaled|説明scaled}}"),
            ("counterweighted", "説明counterweighted"));

        Assert.Multiple(() =>
        {
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "{{Y|masterwork}} steel long sword",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("{{Y|接頭辞masterwork}} 鋼のロングソード"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "{{electrical|electrified}} steel long sword",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("{{electrical|接頭辞electrified}} 鋼のロングソード"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "{{scaled|scaled}} steel long sword",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("{{scaled|接頭辞scaled}} 鋼のロングソード"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "counterweighted 鋼のロングソード",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("接頭辞counterweighted 鋼のロングソード"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "counterweighted(2) 鋼のロングソード",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("接頭辞counterweighted(2) 鋼のロングソード"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "masterwork scoped 鋼のロングソード",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("接頭辞masterwork 接頭辞scoped 鋼のロングソード"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "masterwork scoped electrified 鋼のロングソード",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("接頭辞masterwork 接頭辞scoped 接頭辞electrified 鋼のロングソード"));
        });
    }

    [Test]
    public void TranslatePreservingColors_PreservesSeparatorsAcrossProducerDerivedLongPrefixChains()
    {
        WriteContextDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("{{B|wet}}", "GetDisplayName.Adjective", "{{B|接頭辞wet}}"),
            ("{{slimy|slimy}}", "GetDisplayName.Adjective", "{{slimy|接頭辞slimy}}"),
            ("{{r|bloody}}", "GetDisplayName.Adjective", "{{r|接頭辞bloody}}"),
            ("{{Y|masterwork}}", "GetDisplayName.Adjective", "{{Y|接頭辞masterwork}}"),
            ("scoped", "GetDisplayName.Adjective", "接頭辞scoped"),
            ("{{electrical|electrified}}", "GetDisplayName.Adjective", "{{electrical|接頭辞electrified}}"),
            ("{{fiery|flaming}}", "GetDisplayName.Adjective", "{{fiery|接頭辞flaming}}"),
            ("{{freezing|freezing}}", "GetDisplayName.Adjective", "{{freezing|接頭辞freezing}}"),
            ("{{freezing|frozen}}", "GetDisplayName.Adjective", "{{freezing|接頭辞frozen}}"),
            ("{{painted|painted}}", "GetDisplayName.Adjective", "{{painted|接頭辞painted}}"),
            ("{{lacquered|lacquered}}", "GetDisplayName.Adjective", "{{lacquered|接頭辞lacquered}}"),
            ("{{phase-harmonic|phase-harmonic}}", "GetDisplayName.Adjective", "{{phase-harmonic|接頭辞phase-harmonic}}"),
            ("[empty]", "GetDisplayName.Adjective", "[空]"),
            ("steel long sword", null, "鋼のロングソード"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{B|wet}} {{slimy|slimy}} {{r|bloody}} {{Y|masterwork}} scoped {{electrical|electrified}} {{fiery|flaming}} {{freezing|freezing}} {{freezing|frozen}} {{painted|painted}} {{lacquered|lacquered}} {{phase-harmonic|phase-harmonic}} steel long sword \u001a8 \u00031d6 [empty]",
            nameof(GetDisplayNamePatch));

        Assert.That(
            translated,
            Is.EqualTo("{{B|接頭辞wet}} {{slimy|接頭辞slimy}} {{r|接頭辞bloody}} {{Y|接頭辞masterwork}} 接頭辞scoped {{electrical|接頭辞electrified}} {{fiery|接頭辞flaming}} {{freezing|接頭辞freezing}} {{freezing|接頭辞frozen}} {{painted|接頭辞painted}} {{lacquered|接頭辞lacquered}} {{phase-harmonic|接頭辞phase-harmonic}} 鋼のロングソード {{c|\u001a}}8 {{r|\u0003}}1d6 [空]"));
    }

    [Test]
    public void TranslatePreservingColors_PreservesUnknownModifierInProducerDerivedPrefixChain()
    {
        WriteContextDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("{{B|wet}}", "GetDisplayName.Adjective", "{{B|接頭辞wet}}"),
            ("{{Y|masterwork}}", "GetDisplayName.Adjective", "{{Y|接頭辞masterwork}}"),
            ("[empty]", "GetDisplayName.Adjective", "[空]"),
            ("steel long sword", null, "鋼のロングソード"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{B|wet}} {{unmapped|slick}} {{Y|masterwork}} steel long sword [empty]",
            nameof(GetDisplayNamePatch));

        Assert.That(
            translated,
            Is.EqualTo("{{B|接頭辞wet}} {{unmapped|slick}} {{Y|接頭辞masterwork}} 鋼のロングソード [空]"));
    }

    [Test]
    public void TranslatePreservingColors_PreservesDirectMarkerSegmentInProducerDerivedPrefixChain()
    {
        WriteContextDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("{{B|wet}}", "GetDisplayName.Adjective", "{{B|接頭辞wet}}"),
            ("[empty]", "GetDisplayName.Adjective", "[空]"),
            ("steel long sword", null, "鋼のロングソード"));
        var source = MessageFrameTranslator.DirectTranslationMarker
            + "{{B|wet}} steel long sword [empty]";

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(source));
    }

    [Test]
    public void TranslatePreservingColors_UsesDisplayNameAdjectiveContextForBracketedMarkupWeaponModifiers()
    {
        WriteContextDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("{{Y|masterwork}}", "GetDisplayName.Adjective", "{{Y|接頭辞masterwork}}"),
            ("steel long sword", null, "鋼のロングソード"));
        WriteDictionaryFile(
            "world-mods.ja.json",
            ("{{Y|masterwork}}", "{{Y|説明masterwork}}"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "[{{Y|masterwork}}] steel long sword",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("[{{Y|接頭辞masterwork}}] 鋼のロングソード"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesProducerDerivedBracketedPrefixModifiers()
    {
        WriteContextDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[illuminated]", "GetDisplayName.Adjective", "[{{illuminated|接頭辞illuminated}}]"),
            ("steel long sword", null, "鋼のロングソード"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "[{{illuminated|illuminated}}] steel long sword",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("[{{illuminated|接頭辞illuminated}}] 鋼のロングソード"));
    }

    [TestCase("pair of ナインフォールドのブーツ", "ナインフォールドのブーツ")]
    [TestCase("pair of unknown boots", "pair of unknown boots")]
    [TestCase("pair of ナインフォールド unknown boots", "pair of ナインフォールド unknown boots")]
    public void TranslatePreservingColors_HandlesRuntimePairOfPrefix(string source, string expected)
    {
        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo(expected));
    }

    [Test]
    public void TranslatePreservingColors_SuppressesIdentityVisageMissingKeyNoise()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("VISAGE", "VISAGE"));

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{visage|VISAGE}}",
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.EqualTo("{{visage|VISAGE}}"));
            Assert.That(Translator.GetMissingKeyHitCountForTests("VISAGE"), Is.EqualTo(0));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesAllKnownGamePrefixModifierPhrases()
    {
        UseProductionDictionaries();

        Assert.Multiple(() =>
        {
            foreach (var testCase in KnownGamePrefixModifierPhrases())
            {
                var source = testCase.Source + " チェーンピストル";
                var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(source, nameof(GetDisplayNamePatch));

                Assert.That(translated, Does.Contain(testCase.Expected), testCase.Id);
                Assert.That(translated, Does.EndWith("チェーンピストル"), testCase.Id);
                Assert.That(
                    ColorShapeCaptureObservability.Capture(
                        nameof(InventoryLineTranslationPatch),
                        testCase.Id,
                        source,
                        translated).MarkupSemanticStatus,
                    Is.EqualTo("clean"),
                    testCase.Id);
            }
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesArbitraryLengthAdjectiveChainPreservingColorTags()
    {
        UseProductionDictionaries();

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "{{K|deactivated}} spring-loaded {{w|wooden}} {{c|mechanical}} チェーンピストル",
            nameof(GetDisplayNamePatch));

        Assert.That(
            translated,
            Is.EqualTo("{{K|停止中の}} バネ仕掛けの {{w|木製の}} {{c|機械化}} チェーンピストル"));
        Assert.That(
            ColorShapeCaptureObservability.Capture(
                nameof(InventoryLineTranslationPatch),
                "ArbitraryLengthAdjectiveChain",
                "{{K|deactivated}} spring-loaded {{w|wooden}} {{c|mechanical}} チェーンピストル",
                translated).MarkupSemanticStatus,
            Is.EqualTo("clean"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesModifierChainBeforeLocalizedChargeStatus()
    {
        UseProductionDictionaries();

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            "deactivated spring-loaded {{ninefold|ナインフォールド}}のブーツ {{y|({{G|残量十分}})}}",
            nameof(GetDisplayNamePatch));

        Assert.That(
            translated,
            Is.EqualTo("停止中の バネ仕掛けの{{ninefold|ナインフォールド}}のブーツ {{y|({{G|残量十分}})}}"));
    }

    [Test]
    public void TranslatePreservingColors_TranslatesColoredStainedModifierChainWithoutShiftingCellTags()
    {
        UseProductionDictionaries();

        const string source =
            "{{r|blood}}-stained {{K|deactivated}} spring-loaded {{ninefold|ナインフォールド}}のブーツ \u00041 \t-1 [ケムセル (残量半分) <BD1>] <A12346>";

        var translated = GetDisplayNameRouteTranslator.TranslatePreservingColors(
            source,
            nameof(GetDisplayNamePatch));

        Assert.Multiple(() =>
        {
            Assert.That(translated, Does.Contain("{{r|血}}に染まった {{K|停止中の}} バネ仕掛けの{{ninefold|ナインフォールド}}のブーツ"));
            Assert.That(translated, Does.Contain("[{{c|ケムセル}} {{y|("));
            Assert.That(translated, Does.Contain("{{y|<{{G|B}}{{C|D}}{{r|1}}>}}]"));
            Assert.That(translated, Does.Contain("{{y|<{{R|A}}{{r|1}}{{g|2}}{{b|3}}{{c|4}}{{g|6}}>}}"));
            Assert.That(translated, Does.Not.Contain("blood"));
            Assert.That(translated, Does.Not.Contain("deactivated"));
            Assert.That(translated, Does.Not.Contain("ケムセ{{ninefold|ル"));
            Assert.That(translated, Does.Not.Contain("<BD}}1"));
        });
    }

    [Test]
    public void TranslatePreservingColors_TranslatesDisguiseDisplayNameClauses()
    {
        WriteDictionary(
            ("canvas cloak", "キャンバスの外套"),
            ("snapjaw mask", "スナップジョーの仮面"),
            ("snapjaw", "スナップジョー"));

        Assert.Multiple(() =>
        {
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "snapjaw mask and disguise",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("スナップジョーの仮面（変装）"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "canvas cloak and snapjaw disguise",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("キャンバスの外套（スナップジョーの変装）"));
        });
    }

    [Test]
    public void DisplayNameCaptureTranslator_StripsDirectMarkerBeforeDisplayNameRouteTranslation()
    {
        WriteDictionary(("sword", "剣"));
        WriteDictionaryFile("ui-liquids.ja.json", ("blood", "血"));

        var translated = DisplayNameCaptureTranslator.TranslatePreservingColors(
            MessageFrameTranslator.DirectTranslationMarker + "{{r|blood}}-stained sword",
            nameof(GetDisplayNamePatch));

        Assert.That(translated, Is.EqualTo("{{r|血}}に染まった剣"));
    }

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        WriteDictionaryFile("ui-displayname-route.ja.json", entries);
    }

    private static void UseProductionDictionaries()
    {
        var localizationRoot = ProductionLocalizationRoot();
        LocalizationAssetResolver.SetLocalizationRootForTests(localizationRoot);
        Translator.SetDictionaryDirectoryForTests(Path.Combine(localizationRoot, "Dictionaries"));
    }

    private static string ProductionLocalizationRoot()
    {
        return Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Localization"));
    }

    private static IEnumerable<ModifierPhraseCase> KnownGameWithClauseModifierPhrases()
    {
        yield return new ModifierPhraseCase("ModBeamsplitter", "{{R-R-r-r-g-g-G-G-B-B-b-b sequence|beamsplitter}}", "{{R-R-r-r-g-g-G-G-B-B-b-b sequence|ビームスプリッタ装着}}");
        yield return new ModifierPhraseCase("ModFilters", "{{Y|filters}}", "{{Y|フィルター付き}}");
        yield return new ModifierPhraseCase("ModSuspensor", "{{watery|suspensors}}", "{{watery|サスペンサー付き}}");
        yield return new ModifierPhraseCase("ModCleated", "cleats", "スパイク付き");
        yield return new ModifierPhraseCase("ModPiping", "piping", "配管");
        yield return new ModifierPhraseCase("ModHardened", "{{mercurial|electromagnetic}} shielding", "{{mercurial|電磁}}シールド");
        yield return new ModifierPhraseCase("ModGearbox", "gearbox", "ギアボックス");
        yield return new ModifierPhraseCase("ModCoProcessor", "{{brainbrine|co-processor}}", "{{brainbrine|コプロセッサ}}");
        yield return new ModifierPhraseCase("ModQuantumReverb", "{{quantumreverb|quantum reverb}}", "{{quantumreverb|量子リバーブ}}");
        yield return new ModifierPhraseCase("ModTerrifyingVisage", "{{K|terrifying}} visage", "{{K|恐怖の}}顔貌");
        yield return new ModifierPhraseCase("ModSereneVisage", "{{Y|serene}} visage", "{{Y|静穏}}の顔貌");
    }

    private static IEnumerable<ModifierPhraseCase> KnownGamePrefixModifierPhrases()
    {
        yield return new ModifierPhraseCase("ModAirfoil", "{{Y|airfoil}}", "{{Y|翼型}}");
        yield return new ModifierPhraseCase("ModAntiGravity", "{{B|anti-gravity}}", "{{B|反重力}}");
        yield return new ModifierPhraseCase("ModBiomech", "{{biomech|biomech}}", "{{biomech|バイオメック}}");
        yield return new ModifierPhraseCase("ModCamo", "{{camouflage|camo}}", "{{camouflage|迷彩}}");
        yield return new ModifierPhraseCase("ModCounterweighted", "counterweighted(2)", "つり合い調整(2)");
        yield return new ModifierPhraseCase("ModCybrid", "{{biomech|cybrid}}", "{{biomech|サイブリッド}}");
        yield return new ModifierPhraseCase("ModDefib", "{{love|defib}}", "{{love|除細動}}");
        yield return new ModifierPhraseCase("ModDesecrated", "{{K|desecrated}}", "{{K|冒涜された}}");
        yield return new ModifierPhraseCase("ModDisplacer", "{{displacer|displacer}}", "{{displacer|位相転移}}");
        yield return new ModifierPhraseCase("ModDrumLoaded", "drum-loaded", "ドラム弾倉");
        yield return new ModifierPhraseCase("ModElectrified", "{{electrical|electrified}}", "{{electrical|帯電}}");
        yield return new ModifierPhraseCase("ModEngraved", "{{engraved|engraved}}", "{{engraved|彫り刻まれた}}");
        yield return new ModifierPhraseCase("ModExtradimensional", "{{extradimensional|extradimensional}}", "{{extradimensional|異次元}}");
        yield return new ModifierPhraseCase("ModFeathered", "{{feathered|feathered}}", "{{feathered|羽飾り}}");
        yield return new ModifierPhraseCase("ModFlaming", "{{fiery|flaming}}", "{{fiery|燃え盛る}}");
        yield return new ModifierPhraseCase("ModFlareCompensating", "{{K|flare-compensating}}", "{{K|フレア補償}}");
        yield return new ModifierPhraseCase("ModFlexiweaved", "flexiweaved(2)", "フレキシ織りの(2)");
        yield return new ModifierPhraseCase("ModFreezing", "{{freezing|freezing}}", "{{freezing|凍結した}}");
        yield return new ModifierPhraseCase("ModGesticulating", "{{m|gesticulating}}", "{{m|蠢く}}");
        yield return new ModifierPhraseCase("ModHUD", "HUD", "HUD");
        yield return new ModifierPhraseCase("ModHeartstopper", "{{lovesickness|heartstopper}}", "{{lovesickness|ハートストッパー}}");
        yield return new ModifierPhraseCase("ModHeatSeeking", "homing", "誘導");
        yield return new ModifierPhraseCase("ModHighCapacity", "{{c|high-capacity}}", "{{c|大容量}}");
        yield return new ModifierPhraseCase("ModHypervelocity", "{{hypervelocity|hypervelocity}}", "{{hypervelocity|超高速}}");
        yield return new ModifierPhraseCase("ModIlluminated", "[{{illuminated|illuminated}}]", "[{{illuminated|彩飾}}]");
        yield return new ModifierPhraseCase("ModInduction", "{{Y|induction}}", "{{Y|誘導}}");
        yield return new ModifierPhraseCase("ModJacked", "{{c|jacked}}", "{{c|ジャック付き}}");
        yield return new ModifierPhraseCase("ModJewelEncrusted", "{{m-G-R-W-c-y-B-m-r-W-r-W-c-R-b sequence|jewel-encrusted}}", "{{m-G-R-W-c-y-B-m-r-W-r-W-c-R-b sequence|宝石をちりばめた}}");
        yield return new ModifierPhraseCase("ModKeen", "keen", "鋭利な");
        yield return new ModifierPhraseCase("ModLacquered", "{{lacquered|lacquered}}", "{{lacquered|漆仕上げ}}");
        yield return new ModifierPhraseCase("ModLanterned", "{{lanterned|lanterned}}", "{{lanterned|灯り付き}}");
        yield return new ModifierPhraseCase("ModLegendary", "{{Y|lege{{W|n}}dary}}", "{{Y|伝説{{W|的}}}}");
        yield return new ModifierPhraseCase("ModLiquidCooled", "{{K|liquid-cooled}}", "{{K|液冷式}}");
        yield return new ModifierPhraseCase("ModMagnetized", "magnetized", "磁化した");
        yield return new ModifierPhraseCase("ModMassivelyOverloaded", "{{overloaded|massively overloaded}}", "{{overloaded|重過負荷}}");
        yield return new ModifierPhraseCase("ModMasterwork", "{{Y|masterwork}}", "{{Y|傑作}}");
        yield return new ModifierPhraseCase("ModMercurial", "{{Y|mercurial}}", "{{Y|水銀の}}");
        yield return new ModifierPhraseCase("ModMetallized", "{{c|metallized}}", "{{c|金属化}}");
        yield return new ModifierPhraseCase("ModMetered", "{{c|metered}}", "{{c|計量式}}");
        yield return new ModifierPhraseCase("RoboticizedMechanical", "{{c|mechanical}}", "{{c|機械化}}");
        yield return new ModifierPhraseCase("ModMicroserrated", "{{Y|mi{{R|c}}roserra{{R|t}}ed}}", "{{Y|{{R|微}}鋸{{R|歯}}}}");
        yield return new ModifierPhraseCase("ModMighty", "mighty", "強力な");
        yield return new ModifierPhraseCase("ModMorphogenetic", "{{m|morphogenetic}}", "{{m|形態同調}}");
        yield return new ModifierPhraseCase("ModNanochelated", "{{K|nanochelated}}", "{{K|ナノキレート}}");
        yield return new ModifierPhraseCase("ModNanon", "{{K|nanon}}", "{{K|ナノ刃}}");
        yield return new ModifierPhraseCase("ModNav", "{{r|nav}}", "{{r|航法}}");
        yield return new ModifierPhraseCase("ModNulling", "{{K|nulling}}", "{{K|無効化}}");
        yield return new ModifierPhraseCase("ModOrthopedic", "orthopedic", "整形");
        yield return new ModifierPhraseCase("ModOverbuilt", "overbuilt", "過剰設計の");
        yield return new ModifierPhraseCase("ModOverloaded", "{{overloaded|overloaded}}", "{{overloaded|過負荷}}");
        yield return new ModifierPhraseCase("ModPadded", "padded", "パッド入り");
        yield return new ModifierPhraseCase("ModPainted", "{{painted|painted}}", "{{painted|彩色された}}");
        yield return new ModifierPhraseCase("ModPhaseConjugate", "{{K|phase-conjugate}}", "{{K|位相共役}}");
        yield return new ModifierPhraseCase("ModPhaseHarmonic", "{{phase-harmonic|phase-harmonic}}", "{{phase-harmonic|位相調和}}");
        yield return new ModifierPhraseCase("ModPolarized", "{{polarized|polarized}}", "{{polarized|偏光性}}");
        yield return new ModifierPhraseCase("ModPsionic", "{{psionic|psionic}}", "{{psionic|サイオニック}}");
        yield return new ModifierPhraseCase("PowerSwitchInactive", "deactivated", "停止中の");
        yield return new ModifierPhraseCase("PowerSwitchInactiveColored", "{{K|deactivated}}", "{{K|停止中の}}");
        yield return new ModifierPhraseCase("ModRadioPowered", "{{C|radio-powered}}", "{{C|無線駆動の}}");
        yield return new ModifierPhraseCase("ModRecycling", "{{B|recycling}}", "{{B|再生処理}}");
        yield return new ModifierPhraseCase("ModRefractive", "{{refractive|refractive}}", "{{refractive|屈折性}}");
        yield return new ModifierPhraseCase("ModReinforced", "reinforced", "補強");
        yield return new ModifierPhraseCase("ModScaled", "{{scaled|scaled}}", "{{scaled|鱗状の}}");
        yield return new ModifierPhraseCase("ModScoped", "scoped", "スコープ付き");
        yield return new ModifierPhraseCase("ModSerrated", "{{Y|serra{{R|t}}ed}}", "{{Y|鋸歯{{R|状}}の}}");
        yield return new ModifierPhraseCase("ModSharp", "sharp", "鋭利");
        yield return new ModifierPhraseCase("ModSixFingered", "{{G|six-fingered}}", "{{G|六指の}}");
        yield return new ModifierPhraseCase("ModSlender", "slender", "細身な");
        yield return new ModifierPhraseCase("ModSmart", "{{c|smart}}", "{{c|スマートな}}");
        yield return new ModifierPhraseCase("ModSnailEncrusted", "{{snail-encrusted|snail-encrusted}}", "{{snail-encrusted|巻貝まみれの}}");
        yield return new ModifierPhraseCase("ModSpiked", "{{spiked|spiked}}", "{{spiked|トゲ付き}}");
        yield return new ModifierPhraseCase("ModSpringLoaded", "spring-loaded", "バネ仕掛けの");
        yield return new ModifierPhraseCase("ModSturdy", "sturdy", "頑丈な");
        yield return new ModifierPhraseCase("ModTwoFaced", "two-faced", "双面の");
        yield return new ModifierPhraseCase("ModUrbanCamo", "{{urban camouflage|urban camo}}", "{{urban camouflage|都市迷彩}}");
        yield return new ModifierPhraseCase("ModVisored", "visored", "バイザー付き");
        yield return new ModifierPhraseCase("ModWeightless", "{{y-K sequence|weightless}}", "{{y-K sequence|無重量}}");
        yield return new ModifierPhraseCase("ModWillowy", "willowy", "しなやかな");
        yield return new ModifierPhraseCase("DataAuthoredWooden", "wooden", "木製の");
        yield return new ModifierPhraseCase("DataAuthoredWoodenColored", "{{w|wooden}}", "{{w|木製の}}");
        yield return new ModifierPhraseCase("ModWired", "{{c|wired}}", "{{c|有線}}");
        yield return new ModifierPhraseCase("ModWooly", "{{Y|wooly}}", "{{Y|毛皮張り}}");
    }

    private sealed class ModifierPhraseCase
    {
        public ModifierPhraseCase(string id, string source, string expected)
        {
            Id = id;
            Source = source;
            Expected = expected;
        }

        public string Id { get; }

        public string Source { get; }

        public string Expected { get; }
    }

    private void WriteDictionaryFile(string fileName, params (string key, string text)[] entries)
    {
        WriteDictionaryFile(fileName, BuildJsonEntriesDocument(entries));
    }

    private void WriteDictionaryFile(string fileName, string contents)
    {
        var path = Path.Combine(tempDirectory, fileName);
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        File.WriteAllText(
            path,
            contents,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WriteAliasFile(params (string key, string text)[] entries)
    {
        LocalizationAssetResolver.SetLocalizationRootForTests(tempDirectory);
        WriteJsonEntriesFile(Path.Combine(tempDirectory, "Aliases", "displayname-legacy-aliases.json"), entries);
    }

    private static void WriteJsonEntriesFile(string path, params (string key, string text)[] entries)
    {
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        File.WriteAllText(
            path,
            BuildJsonEntriesDocument(entries),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string BuildJsonEntriesDocument(IReadOnlyList<(string key, string text)> entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"entries\":[");

        for (var index = 0; index < entries.Count; index++)
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

        return builder.ToString();
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

        WriteDictionaryFile(fileName, builder.ToString());
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
