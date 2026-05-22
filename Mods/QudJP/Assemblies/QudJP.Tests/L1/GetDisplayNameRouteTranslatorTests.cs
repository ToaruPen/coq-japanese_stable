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
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
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
            "クローム・リボルバー {{c|\u001a}}7 {{r|\u0003}}1d6 {{y|[鉛スラッグ x6]}} [{{r|rusted}}] [{{r|broken}}] {{y|<{{|{{B|C}}{{B|C}}{{g|2}}}}>}}",
            nameof(GetDisplayNamePatch));

        Assert.That(
            translated,
            Is.EqualTo("クローム・リボルバー {{c|\u001a}}7 {{r|\u0003}}1d6 {{y|[鉛スラッグ x6]}} [{{r|錆びた}}] [{{r|破損}}] {{y|<{{|{{B|C}}{{B|C}}{{g|2}}}}>}}"));
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
            Is.EqualTo("クローム・リボルバー {{c|\u001a}}7 {{r|\u0003}}1d6 {{y|[鉛スラッグ x6]}} [{{r|錆びた}}] {{y|<{{|{{B|C}}{{B|C}}{{g|2}}}}>}}"));
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
            Is.EqualTo("レーザーライフル（{{Y|フィルター付き}}） {{c|\u001a}}8 {{r|\u0003}}1d12 {{y|<{{|{{B|A}}{{B|D}}{{g|1}}{{g|4}}}}>}}"));

        Assert.That(
            GetDisplayNameRouteTranslator.TranslatePreservingColors(
                "laser rifle with {{Y|filters}} {{W|\u001a}}8 {{r|\u0003}}1d12 {{y|<{{|{{B|A}}{{B|D}}{{g|1}}{{g|4}}}}>}}",
                nameof(GetDisplayNamePatch)),
            Is.EqualTo("レーザーライフル（{{Y|フィルター付き}}） {{W|\u001a}}8 {{r|\u0003}}1d12 {{y|<{{|{{B|A}}{{B|D}}{{g|1}}{{g|4}}}}>}}"));
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
            Assert.That(translated, Is.EqualTo("水袋 [{{K|空}}]"));
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
                Is.EqualTo("スナップジョー [{{B|浅瀬を進んでいる}}]"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "banner {{y|[{{g|raised}}]}}",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("旗 [{{g|掲揚中}}]"));
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
                Is.EqualTo("地雷 [{{R|10秒}}]"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "ingredient {{y|[{{C|3}} cooking servings]}}",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("食材 [調理3回分]"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "rack {{y|[2 cells]}}",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("ラック [セル2個]"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "deed {{y|[Hindren chapter]}}",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("証書 [ヒンドレン支部]"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "magazine {{y|[lead slug]}}",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("マガジン [鉛スラッグ]"));
            Assert.That(
                GetDisplayNameRouteTranslator.TranslatePreservingColors(
                    "snapjaw [{{B|stuck in a web}}]",
                    nameof(GetDisplayNamePatch)),
                Is.EqualTo("スナップジョー [{{B|網にはまっている}}]"));
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

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        WriteDictionaryFile("ui-displayname-route.ja.json", entries);
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

        File.WriteAllText(
            path,
            contents,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
