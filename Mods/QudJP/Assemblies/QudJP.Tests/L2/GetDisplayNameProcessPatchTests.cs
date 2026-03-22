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
        WriteDictionary(
            ("bloody", "{{r|血まみれの}}"),
            ("village apothecary", "村の薬師"));

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

    [TestCase("水たまり of salty water", "塩気のある水の水たまり")]
    [TestCase("水たまり of brackish blood", "塩気混じりの血の水たまり")]
    public void Postfix_TranslatesLiquidPrepositionDisplayName_WhenPatched(string source, string expected)
    {
        WriteDictionary(
            ("salty", "塩気のある"),
            ("brackish", "塩気混じりの"),
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
            ("sitting on {0}", "{0}に座っている"),
            ("chair", "椅子"));

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
        WriteDictionary(("[empty]", "[空]"));

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
    public void Postfix_TranslatesGeneratedProperNameModifier_WhenPatched()
    {
        WriteDictionary(("bloody", "{{r|血まみれの}}"));

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
        File.WriteAllText(
            Path.Combine(tempDirectory, fileName),
            contents,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
