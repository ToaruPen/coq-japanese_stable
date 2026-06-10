using System;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using NUnit.Framework;
using QudJP.Patches;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class GetDisplayNameProcessPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-displayname-process-l2", Guid.NewGuid().ToString("N"));
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
    public void Postfix_TranslatesKnownDisplayName_WhenPatched()
    {
        WriteDictionary(("engraved carbide dagger", "刻印されたカーバイドダガー"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("engraved carbide dagger");

            Assert.That(result, Is.EqualTo("刻印されたカーバイドダガー"));
        });
    }

    [Test]
    public void Postfix_TranslatesMixedModifierAndJapaneseBase_WhenPatched()
    {
        WriteDictionary(("lacquered", "漆塗り"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("lacquered サンダル");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("漆塗りサンダル"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("lacquered サンダル"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesEnglishTitlePrefixAndJapaneseBase_WhenPatched()
    {
        WriteDictionary(("Warden", "監視官"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("Warden イラメ");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("監視官イラメ"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("Warden イラメ"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesGeneratedTitleSuffix_WhenPatched()
    {
        WriteDictionary(("village apothecary", "村の薬師"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("Naruur, village apothecary");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("Naruur、村の薬師"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("Naruur, village apothecary"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesGeneratedTinkerTitleSuffix_WhenPatched()
    {
        WriteDictionary(("the village tinker", "村の修理工"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("Uukat, the village tinker");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("Uukat、村の修理工"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("Uukat, the village tinker"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesGeneratedMerchantTitleSuffix_WhenPatched()
    {
        WriteDictionary(("the village merchant", "村の商人"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("Yurl, the village merchant");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("Yurl、村の商人"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("Yurl, the village merchant"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesGeneratedDromadMerchantTitleSuffix_WhenPatched()
    {
        WriteDictionary(("dromad merchant", "ドロマド商人"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("タム, dromad merchant");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("タム、ドロマド商人"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("タム, dromad merchant"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesGeneratedLastSultanTitleSuffix_WhenPatched()
    {
        WriteDictionary(("the Last Sultan", "最後のスルタン"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("レシェフの神殿, the Last Sultan");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("レシェフの神殿、最後のスルタン"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("レシェフの神殿, the Last Sultan"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesGeneratedTitleSuffixWithLeadingModifier_WhenPatched()
    {
        WriteDictionary(("village apothecary", "村の薬師"));
        WriteDictionaryFile("ui-displayname-adjectives.ja.json", ("bloody", "{{r|血まみれの}}"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("bloody Naruur, village apothecary");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("{{r|血まみれの}}Naruur、村の薬師"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("bloody Naruur, village apothecary"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesHistoricSpiceFakedDeathCognomen_WhenPatched()
    {
        WriteDictionaryFile("Scoped/historyspice-common.ja.json", ("desiccated", "乾ききった"), ("spectre", "亡霊"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("the Desiccated Spectre");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("乾ききった亡霊"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("the Desiccated Spectre"), Is.EqualTo(0));
            });
        });
    }

    [TestCase("水たまり of salty water", "塩気のある水の水たまり")]
    [TestCase("水たまり of brackish blood", "塩分混じりの血の水たまり")]
    public void Postfix_TranslatesLiquidPrepositionDisplayName_WhenPatched(string source, string expected)
    {
        WriteDictionary(
            ("salty", "塩気のある"),
            ("brackish", "塩分混じりの"),
            ("water", "水"),
            ("blood", "血"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor(source);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expected));
                Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesBracketedDisplayNameSuffix_WhenPatched()
    {
        WriteDictionary(
            ("dromad merchant", "ドロマド商人"),
            ("sitting", "座っている"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("タム, dromad merchant [sitting]");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("タム、ドロマド商人 [座っている]"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("タム, dromad merchant [sitting]"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_DoesNotReTranslateAlreadyLocalizedBracketedDisplayName_WhenPatched()
    {
        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("タム、ドロマド商人 [座っている]");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("タム、ドロマド商人 [座っている]"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("タム、ドロマド商人 [座っている]"), Is.EqualTo(0));
                Assert.That(Translator.GetMissingKeyHitCountForTests("[座っている]"), Is.EqualTo(0));
                Assert.That(Translator.GetMissingKeyHitCountForTests("座っている"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesBracketedDisplayNameStateTemplate_WhenPatched()
    {
        WriteDictionary(
            ("dromad merchant", "ドロマド商人"),
            ("chair", "椅子"));
        WriteDictionaryFile(
            "Scoped/ui-displayname-state-templates.ja.json",
            "{\"entries\":[{\"key\":\"sitting on {0}\",\"context\":\"GetDisplayName.StateTemplate\",\"text\":\"{0}に座っている\"}]}\n");

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("タム, dromad merchant [sitting on a chair]");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("タム、ドロマド商人 [椅子に座っている]"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("タム, dromad merchant [sitting on a chair]"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesBracketedInventoryState_WhenPatched()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[empty]", "[空]"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("水袋 [empty]");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("水袋 [空]"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("水袋 [empty]"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesBracketedLiquidAmount_WhenPatched()
    {
        WriteDictionary(("fresh water", "真水"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("水筒 [32 drams of fresh water]");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("水筒 [32ドラムの真水]"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("水筒 [32 drams of fresh water]"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesLocalizedPuddleWithQuantifiedLiquidTail_WhenPatched()
    {
        WriteScopedDictionaryFile(
            "ui-liquid-adjectives.ja.json",
            "XRL.Liquids.Adjective",
            ("{{Y|salty}}", "{{Y|塩気のある}}"),
            ("salty", "塩気のある"));
        WriteScopedDictionaryFile(
            "ui-liquids.ja.json",
            "XRL.Liquids",
            ("{{B|water}}", "{{B|水}}"),
            ("water", "水"),
            ("salty water", "塩水"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("塩水の水たまり of {{rules|500}} drams of {{Y|salty}} {{B|water}}");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("{{rules|500}}ドラムの{{Y|塩気のある}} {{B|水}}の水たまり"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("塩水の水たまり of 500 drams of salty water"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesBarePuddleWithQuantifiedLiquidTailPreservingLiquidColors_WhenPatched()
    {
        WriteScopedDictionaryFile(
            "ui-liquid-adjectives.ja.json",
            "XRL.Liquids.Adjective",
            ("{{Y|salty}}", "{{Y|塩気のある}}"),
            ("salty", "塩気のある"));
        WriteScopedDictionaryFile(
            "ui-liquids.ja.json",
            "XRL.Liquids",
            ("{{r|blood}}", "{{r|血}}"),
            ("blood", "血"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("水たまり of 1 dram of {{Y|salty}} {{r|blood}}");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("1ドラムの{{Y|塩気のある}} {{r|血}}の水たまり"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("水たまり of 1 dram of salty blood"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesPuddleWithChainedLiquidAdjectives_WhenPatched()
    {
        WriteScopedDictionaryFile(
            "ui-liquid-adjectives.ja.json",
            "XRL.Liquids.Adjective",
            ("brackish", "塩分混じりの"),
            ("bloody", "血混じりの"));
        WriteScopedDictionaryFile("ui-liquids.ja.json", "XRL.Liquids", ("water", "水"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("水たまり of 7 drams of brackish bloody water");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("7ドラムの塩分混じりの血混じりの水の水たまり"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("水たまり of 7 drams of brackish bloody water"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesMarkedBrackishWaterPuddleAsLiquidAdjective_WhenPatched()
    {
        WriteScopedDictionaryFile(
            "ui-liquid-adjectives.ja.json",
            "XRL.Liquids.Adjective",
            ("brackish", "塩分混じりの"));
        WriteScopedDictionaryFile(
            "ui-liquids.ja.json",
            "XRL.Liquids",
            ("{{B|water}}", "{{B|水}}"),
            ("water", "水"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("水たまり of {{rules|500}} drams of {{w|brackish}} {{B|water}}");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("{{rules|500}}ドラムの{{w|塩分混じりの}} {{B|水}}の水たまり"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("水たまり of 500 drams of brackish water"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesBracketedLiquidAmountWithState_WhenPatched()
    {
        WriteDictionaryFile(
            "ui-liquids.ja.json",
            ("algal", "{{g|藻質の}}"),
            ("convalessence", "{{C|コンバレセンス}}"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[sealed]", "[密封]"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("宙吊りのシーシャ [3 drams of algal convalessence, sealed]");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("宙吊りのシーシャ [3ドラムの{{g|藻質の}}{{C|コンバレセンス}}、密封]"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("algal convalessence, sealed"), Is.EqualTo(0));
                Assert.That(Translator.GetMissingKeyHitCountForTests("[sealed]"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesLiquidStateWithoutQuantity_WhenPatched()
    {
        WriteDictionaryFile(
            "ui-liquids.ja.json",
            ("algal", "{{g|藻質の}}"),
            ("convalessence", "{{C|コンバレセンス}}"));
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("[sealed]", "[密封]"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("algal convalessence, sealed");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("{{g|藻質の}}{{C|コンバレセンス}}、密封"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("algal convalessence, sealed"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesAlgalWaterLiquidState_WhenPatched()
    {
        WriteDictionary(
            ("algal", "藻入り"),
            ("water", "水"));
        WriteDictionaryFile("ui-displayname-adjectives.ja.json", ("[sealed]", "[密封]"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("algal water, sealed");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("藻入り水、密封"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("algal water, sealed"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesLiquidStateWithGeneralDictionaryFallback_WhenPatched()
    {
        WriteDictionary(("fresh water", "真水"));
        WriteDictionaryFile("ui-displayname-adjectives.ja.json", ("[sealed]", "[密封]"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("fresh water, sealed");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("真水、密封"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("fresh water, sealed"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesLocalizedBaseWithPipingClause_WhenPatched()
    {
        WriteDictionaryFile("ui-displayname-adjectives.ja.json", ("with piping", "（配管付き）"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("シーシャ with piping");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("シーシャ（配管付き）"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("シーシャ with piping"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesLeadingMarkupWrappedModifier_WhenPatched()
    {
        WriteDictionary(("{{graffitied|graffitied}}", "{{graffitied|落書きされた}}"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("{{graffitied|graffitied}} 塩漬け茎の壁");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("{{graffitied|落書きされた}} 塩漬け茎の壁"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("{{graffitied|graffitied}} 塩漬け茎の壁"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesLeadingMarkupWrappedModifierFromVisibleDictionary_WhenPatched()
    {
        WriteDictionaryFile("ui-displayname-adjectives.ja.json", ("bloody", "血まみれの"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("{{r|bloody}} 塩漬け茎の壁");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("{{r|血まみれの}} 塩漬け茎の壁"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("{{r|bloody}} 塩漬け茎の壁"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesGeneratedProperNameModifier_WhenPatched()
    {
        WriteDictionaryFile("ui-displayname-adjectives.ja.json", ("bloody", "{{r|血まみれの}}"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("bloody Naruur");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("{{r|血まみれの}}Naruur"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("bloody Naruur"), Is.EqualTo(0));
            });
        });
    }

    [TestCase("Lead Slug", "鉛スラッグ")]
    [TestCase("Torch", "たいまつ")]
    [TestCase("Wooden Arrow", "木の矢")]
    public void Postfix_TranslatesAtomicDisplayName_WhenPatched(string source, string expected)
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("Lead Slug", "鉛スラッグ"),
            ("Torch", "たいまつ"),
            ("Wooden Arrow", "木の矢"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor(source);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(expected));
                Assert.That(Translator.GetMissingKeyHitCountForTests(source), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesQuantitySuffix_WhenPatched()
    {
        WriteDictionary(("water flask", "水袋"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("water flask x2");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("水袋 x2"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("water flask x2"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesMkTierSuffix_WhenPatched()
    {
        WriteDictionary(("rusted grenade", "錆びたグレネード"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("rusted grenade mk I <AA1>");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("錆びたグレネード mk I <AA1>"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("rusted grenade mk I <AA1>"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesAngleCodeSuffix_WhenPatched()
    {
        WriteDictionary(("worn bronze sword", "使い込まれた青銅の剣"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("worn bronze sword <BD1>");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("使い込まれた青銅の剣 <BD1>"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("worn bronze sword <BD1>"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesParenthesizedStateSuffix_WhenPatched()
    {
        WriteDictionary(("unburnt", "未使用"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("たいまつ x10 (unburnt)");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("たいまつ x10 (未使用)"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("たいまつ x10 (unburnt)"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesParenthesizedStateAndAngleCodeSuffix_WhenPatched()
    {
        WriteDictionary(("Low", "低"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("ケムセル (Low) <BD1>");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("ケムセル (低) <BD1>"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("ケムセル (Low) <BD1>"), Is.EqualTo(0));
            });
        });
    }

    [TestCase("K", "Drained", "空")]
    [TestCase("r", "Very Low", "残量わずか")]
    [TestCase("R", "Low", "残量少")]
    [TestCase("W", "Used", "残量半分")]
    [TestCase("g", "Fresh", "残量多")]
    [TestCase("G", "Full", "残量十分")]
    [TestCase("C", "Unknown", "Unknown")]
    [TestCase("M", "", "")]
    [TestCase("Y", "\u0001Low", "Low")]
    public void Postfix_TranslatesColoredChargeStateSuffix_WhenPatched(
        string color,
        string state,
        string expectedState)
    {
        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor($"{{{{c|ケムセル}}}} {{{{y|({{{{{color}|{state}}}}})}}}}");

            Assert.That(result, Is.EqualTo($"{{{{c|ケムセル}}}} {{{{y|({{{{{color}|{expectedState}}}}})}}}}"));
        });
    }

    [Test]
    public void Postfix_TranslatesJapaneseEntry_WhenPatched()
    {
        WriteDictionary(("奇妙な遺物", "奇妙なアーティファクト"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("奇妙な遺物");

            Assert.That(result, Is.EqualTo("奇妙なアーティファクト"));
        });
    }

    [Test]
    public void Postfix_PreservesColorCodes_WhenPatched()
    {
        WriteDictionary(("engraved carbide dagger", "刻印されたカーバイドダガー"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("{{C|engraved carbide dagger}}");

            Assert.That(result, Is.EqualTo("{{C|刻印されたカーバイドダガー}}"));
        });
    }

    [Test]
    public void Postfix_PassesThroughUnknownDisplayName_WhenPatched()
    {
        WriteDictionary(("known relic", "既知の遺物"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("unknown relic");

            Assert.That(result, Is.EqualTo("unknown relic"));
        });
    }

    [Test]
    public void Postfix_TranslatesSavedRelicGeneratedDisplayName_WhenPatched()
    {
        WriteDictionaryFile(
            "Scoped/historyspice-common.ja.json",
            ("point", "尖端"),
            ("commanding", "威厳ある"),
            ("woe", "嘆き"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("{{C|the Point of the Commanding Woe}}");

            Assert.That(result, Is.EqualTo("{{C|威厳ある嘆きの尖端}}"));
        });
    }

    [Test]
    public void Postfix_PreservesColorMarkupForLocalizedPrefixRelicGeneratedDisplayName_WhenPatched()
    {
        WriteDictionaryFile(
            "Scoped/historyspice-common.ja.json",
            ("point", "尖端"),
            ("commanding", "威厳ある"),
            ("woe", "嘆き"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("粘液質の {{Y|Point of the Commanding Woe}}");

            Assert.That(result, Is.EqualTo("粘液質の {{Y|威厳ある嘆きの尖端}}"));
        });
    }

    [Test]
    public void Postfix_PreservesTmpColorMarkupForLocalizedPrefixRelicGeneratedDisplayName_WhenPatched()
    {
        WriteDictionaryFile(
            "Scoped/historyspice-common.ja.json",
            ("point", "尖端"),
            ("commanding", "威厳ある"),
            ("woe", "嘆き"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("粘液質の <color=#ff0>Point of the Commanding Woe</color>");

            Assert.That(result, Is.EqualTo("粘液質の <color=#ff0>威厳ある嘆きの尖端</color>"));
        });
    }

    [Test]
    public void Postfix_PreservesQudColorCodesForLocalizedPrefixRelicGeneratedDisplayName_WhenPatched()
    {
        WriteDictionaryFile(
            "Scoped/historyspice-common.ja.json",
            ("point", "尖端"),
            ("commanding", "威厳ある"),
            ("woe", "嘆き"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("粘液質の &YPoint of the Commanding Woe&y");

            Assert.That(result, Is.EqualTo("粘液質の &Y威厳ある嘆きの尖端&y"));
        });
    }

    [Test]
    public void Postfix_PreservesStackedQudColorCodesForLocalizedPrefixRelicGeneratedDisplayName_WhenPatched()
    {
        WriteDictionaryFile(
            "Scoped/historyspice-common.ja.json",
            ("point", "尖端"),
            ("commanding", "威厳ある"),
            ("woe", "嘆き"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("粘液質の &Y^rPoint of the Commanding Woe&y^k");

            Assert.That(result, Is.EqualTo("粘液質の &Y^r威厳ある嘆きの尖端&y^k"));
        });
    }

    [Test]
    public void Postfix_TranslatesSavedRelicGeneratedArmorDisplayName_WhenPatched()
    {
        WriteDictionaryFile(
            "Scoped/historyspice-common.ja.json",
            ("embraced", "受け入れた"),
            ("telescope", "望遠鏡"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("{{C|the Breast of the Embraced Telescope}}");

            Assert.That(result, Is.EqualTo("{{C|受け入れた望遠鏡の胸甲}}"));
        });
    }

    [Test]
    public void Postfix_TranslatesSavedRelicGeneratedArmorDisplayNameWithStats_WhenPatched()
    {
        WriteDictionaryFile(
            "Scoped/historyspice-common.ja.json",
            ("embraced", "受け入れた"),
            ("telescope", "望遠鏡"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("{{C|the Breast of the Embraced Telescope}} \x04" + "4 \t2");

            Assert.That(result, Is.EqualTo("{{C|受け入れた望遠鏡の胸甲}} {{b|\x04}}4 {{K|\t}}2"));
        });
    }

    [Test]
    public void Postfix_SkipsMissingKeyLogging_ForFigurineFamily_WhenBuilderMatches()
    {
        WriteDictionary(("手袋屋", "手袋屋"));

        RunWithFigurineDisplayNameProcessPatch(() =>
        {
            var processor = new DummyFigurineDisplayNameProcessor();
            var result = processor.ProcessFor(displayName: "瑪瑙 手袋屋 のフィギュリン");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("瑪瑙 手袋屋 のフィギュリン"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("瑪瑙 手袋屋 のフィギュリン"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TransformsLegendaryFamily_WhenBuilderMatches()
    {
        WriteDictionary(("ヒヒ", "ヒヒ"));

        RunWithFigurineDisplayNameProcessPatch(() =>
        {
            var processor = new DummyFigurineDisplayNameProcessor { DB = new DummyDescriptionBuilder("ヒヒ", "legendary") };
            var result = processor.ProcessFor(displayName: "Oo-hoo-ho-HOO-OOO-ee-ho, legendary ヒヒ");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("Oo-hoo-ho-HOO-OOO-ee-ho、伝説のヒヒ"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("Oo-hoo-ho-HOO-OOO-ee-ho, legendary ヒヒ"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_SkipsMissingKeyLogging_ForWarlordFamily_WhenBuilderMatches()
    {
        WriteDictionary(("スナップジョー", "スナップジョー"));

        RunWithFigurineDisplayNameProcessPatch(() =>
        {
            var processor = new DummyFigurineDisplayNameProcessor { DB = new DummyDescriptionBuilder("スナップジョー", "軍主") };
            var result = processor.ProcessFor(displayName: "スナップジョーの軍主");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("スナップジョーの軍主"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("スナップジョーの軍主"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_LeavesComposedNameWithoutMissingKeyNoise_WhenBuilderLastAddedDoesNotMatch()
    {
        WriteDictionary(("ヒヒ", "ヒヒ"));

        RunWithFigurineDisplayNameProcessPatch(() =>
        {
            var processor = new DummyFigurineDisplayNameProcessor { DB = new DummyDescriptionBuilder("ヒヒ", "warlord") };
            var result = processor.ProcessFor(displayName: "Oo-hoo-ho-HOO-OOO-ee-ho, legendary ヒヒ");

                Assert.Multiple(() =>
                {
                    Assert.That(result, Is.EqualTo("Oo-hoo-ho-HOO-OOO-ee-ho, legendary ヒヒ"));
                    Assert.That(Translator.GetMissingKeyHitCountForTests("Oo-hoo-ho-HOO-OOO-ee-ho, legendary ヒヒ"), Is.EqualTo(0));
                });
            });
    }

    [Test]
    public void Postfix_PrefersDisplayNameScopedDictionary_WhenLiquidAdjectiveKeyConflicts()
    {
        WriteDictionaryFile("ui-displayname-adjectives.ja.json", ("bloody", "{{r|血まみれの}}"));
        WriteDictionaryFile("ui-liquid-adjectives.ja.json", ("bloody", "血混じりの"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("bloody Naruur");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("{{r|血まみれの}}Naruur"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("bloody Naruur"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesLiquidCooledAdjectiveWithLiquidColorMarkup_WhenPatched()
    {
        WriteDictionaryFile("ui-displayname-adjectives.ja.json", ("liquid-cooled", "液冷式"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("{{B|liquid-cooled}}");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("{{B|液冷式}}"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("{{B|liquid-cooled}}"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesGeneratedCanvasTentName_WhenPatched()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            "{\"entries\":[" +
            "{\"key\":\"tortoise\",\"context\":\"GetDisplayName.GeneratedCanvasTent.Component\",\"text\":\"亀\"}," +
            "{\"key\":\"keratin\",\"context\":\"GetDisplayName.GeneratedCanvasTent.Component\",\"text\":\"ケラチン質\"}," +
            "{\"key\":\"dragonfly\",\"context\":\"GetDisplayName.GeneratedCanvasTent.Component\",\"text\":\"トンボ\"}," +
            "{\"key\":\"chitin\",\"context\":\"GetDisplayName.GeneratedCanvasTent.Component\",\"text\":\"キチン質\"}," +
            "{\"key\":\"tent\",\"context\":\"GetDisplayName.GeneratedCanvasTent.Component\",\"text\":\"天幕\"}," +
            "{\"key\":\"salt kraken\",\"text\":\"ソルト・クラーケン\"}," +
            "{\"key\":\"tortoise\",\"text\":\"亀ではない\"}," +
            "{\"key\":\"keratin\",\"text\":\"ケラチンではない\"}," +
            "{\"key\":\"dragonfly\",\"text\":\"トンボではない\"}," +
            "{\"key\":\"chitin\",\"text\":\"キチンではない\"}," +
            "{\"key\":\"tent\",\"text\":\"テントではない\"}" +
            "]}\n");

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("tortoise keratin tent");
            var multiWordCreatureResult = processor.ProcessFor("salt kraken keratin tent");
            var observedRuntimeResult = processor.ProcessFor("dragonfly chitin tent");
            var nonObservedCombinationResult = processor.ProcessFor("tortoise chitin tent");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("亀のケラチン質の天幕"));
                Assert.That(multiWordCreatureResult, Is.EqualTo("ソルト・クラーケンのケラチン質の天幕"));
                Assert.That(observedRuntimeResult, Is.EqualTo("トンボのキチン質の天幕"));
                Assert.That(nonObservedCombinationResult, Is.EqualTo("亀のキチン質の天幕"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("tortoise keratin tent"), Is.EqualTo(0));
                Assert.That(Translator.GetMissingKeyHitCountForTests("salt kraken keratin tent"), Is.EqualTo(0));
                Assert.That(Translator.GetMissingKeyHitCountForTests("dragonfly chitin tent"), Is.EqualTo(0));
                Assert.That(Translator.GetMissingKeyHitCountForTests("tortoise chitin tent"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesGeneratedRandomStatueName_WhenPatched()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            "{\"entries\":[" +
            "{\"key\":\"stone\",\"context\":\"GetDisplayName.GeneratedRandomStatue.Component\",\"text\":\"石\"}," +
            "{\"key\":\"statue\",\"context\":\"GetDisplayName.GeneratedRandomStatue.Component\",\"text\":\"像\"}," +
            "{\"key\":\"stone\",\"text\":\"石ではない\"}," +
            "{\"key\":\"statue\",\"text\":\"像ではない\"}" +
            "]}\n");

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("stone statue of a 山羊人の種播き");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("山羊人の種播きの石の像"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("stone statue of a 山羊人の種播き"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesLocalizedObjectWithPipingSuffix_WhenPatched()
    {
        WriteDictionaryFile(
            "ui-displayname-adjectives.ja.json",
            ("with piping", "（配管付き）"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("宙吊りのシーシャ with piping");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("宙吊りのシーシャ（配管付き）"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("宙吊りのシーシャ with piping"), Is.EqualTo(0));
            });
        });
    }

    [Test]
    public void Postfix_TranslatesObservedAtomicCanvasWall_WhenPatched()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("CanvasWall", "帆布壁"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("CanvasWall");

            Assert.That(result, Is.EqualTo("帆布壁"));
        });
    }

    [Test]
    public void Postfix_TranslatesMinerGeneratedRoleSuffix_WhenPatched()
    {
        WriteDictionaryFile(
            "ui-displayname-atomic.ja.json",
            ("defoliant grenade", "落葉剤グレネード"));

        RunWithDisplayNameProcessPatch(() =>
        {
            var processor = new DummyDisplayNameProcessor();
            var result = processor.ProcessFor("defoliant grenade mk I miner mk I");

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo("落葉剤グレネード mk I 採掘機 mk I"));
                Assert.That(Translator.GetMissingKeyHitCountForTests("defoliant grenade mk I miner mk I"), Is.EqualTo(0));
            });
        });
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

    private void WriteDictionary(params (string key, string text)[] entries)
    {
        WriteDictionaryFile("ui-getdisplayname-process.ja.json", entries);
    }

    private void WriteDictionaryFile(string fileName, params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        AppendEntries(builder, entries);
        builder.AppendLine("]}");
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

    private void WriteScopedDictionaryFile(
        string fileName,
        string context,
        params (string key, string text)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append("{\"entries\":[");
        AppendScopedEntries(builder, context, entries);
        builder.AppendLine("]}");
        WriteDictionaryFile(fileName, builder.ToString());
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

    private static void AppendEntries(StringBuilder builder, IReadOnlyList<(string key, string text)> entries)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var (key, text) = entries[index];
            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(key));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(text));
            builder.Append("\"}");
        }
    }

    private static void AppendScopedEntries(
        StringBuilder builder,
        string context,
        IReadOnlyList<(string key, string text)> entries)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            var (key, text) = entries[index];
            builder.Append("{\"key\":\"");
            builder.Append(EscapeJson(key));
            builder.Append("\",\"context\":\"");
            builder.Append(EscapeJson(context));
            builder.Append("\",\"text\":\"");
            builder.Append(EscapeJson(text));
            builder.Append("\"}");
        }
    }

    private static HarmonyMethod DisplayNameProcessPostfix =>
        new HarmonyMethod(RequireMethod(typeof(GetDisplayNameProcessPatch), nameof(GetDisplayNameProcessPatch.Postfix)));

    private static void RunWithDisplayNameProcessPatch(Action assertion)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyDisplayNameProcessor), nameof(DummyDisplayNameProcessor.ProcessFor)),
                postfix: DisplayNameProcessPostfix);
            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private static void RunWithFigurineDisplayNameProcessPatch(Action assertion)
    {
        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyFigurineDisplayNameProcessor), nameof(DummyFigurineDisplayNameProcessor.ProcessFor)),
                postfix: DisplayNameProcessPostfix);
            assertion();
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    private sealed class DummyDisplayNameProcessor
    {
        public object? DB = new object();

        public string ProcessFor(string displayName)
        {
            _ = DB;
            return displayName;
        }
    }

    private sealed class DummyFigurineDisplayNameProcessor
    {
        public DummyDescriptionBuilder DB = new DummyDescriptionBuilder("手袋屋", "のフィギュリン");

        public string ProcessFor(string displayName)
        {
            _ = string.Concat(DB.PrimaryBase, DB.LastAdded);
            return displayName;
        }
    }

    private sealed class DummyDescriptionBuilder
    {
        public DummyDescriptionBuilder(string primaryBase, string lastAdded)
        {
            PrimaryBase = primaryBase;
            LastAdded = lastAdded;
        }

        public string PrimaryBase;

        public string LastAdded;
    }
}
