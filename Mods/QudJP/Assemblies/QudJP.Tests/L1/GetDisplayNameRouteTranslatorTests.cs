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

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
