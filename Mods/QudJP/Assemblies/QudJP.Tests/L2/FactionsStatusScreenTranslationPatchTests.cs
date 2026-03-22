using System.Reflection;
using System.Text;
using HarmonyLib;
using QudJP.Patches;
using QudJP.Tests;
using QudJP.Tests.DummyTargets;

namespace QudJP.Tests.L2;

[TestFixture]
[Category("L2")]
[NonParallelizable]
public sealed class FactionsStatusScreenTranslationPatchTests
{
    private string tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-factionsstatus-l2", Guid.NewGuid().ToString("N"));
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
    public void FactionsLineDataPostfix_TranslatesLabelAndBuildsBilingualSearchText_WhenPatched()
    {
        WriteDictionary(
            ("The villagers of {0}", "{0}の村人たち"),
            ("The villagers of {0} don't care about you, but aggressive ones will attack you.", "{0}の村人たちはあなたを特に気に掛けていないが、攻撃的な者は襲ってくる。"),
            ("The villagers of {0} are interested in hearing gossip that's about them.", "{0}の村人たちは、自分たちに関するうわさ話に興味を示す。"),
            ("You aren't welcome in their holy places.", "あなたは彼らの聖地では歓迎されていない。"),
            ("You are welcome in their holy places.", "あなたは彼らの聖地で歓迎されている。"));

        var harmonyId = CreateHarmonyId();
        var harmony = new Harmony(harmonyId);

        try
        {
            harmony.Patch(
                original: RequireMethod(typeof(DummyFactionsLineData), nameof(DummyFactionsLineData.set)),
                postfix: new HarmonyMethod(RequireMethod(typeof(FactionsLineDataTranslationPatch), nameof(FactionsLineDataTranslationPatch.Postfix))));

            var data = new DummyFactionsLineData("ignored");
            _ = data.set("Abal", "The villagers of Abal", icon: null, expanded: true);

            Assert.Multiple(() =>
            {
                Assert.That(data.label, Is.EqualTo("Abalの村人たち"));
                Assert.That(data.searchText, Does.Contain("the villagers of abal"));
                Assert.That(data.searchText, Does.Contain("abalの村人たち"));
            });
        }
        finally
        {
            harmony.UnpatchAll(harmonyId);
        }
    }

    [Test]
    public void TranslateFactionText_DoesNotLogAlreadyLocalizedDirectLabel()
    {
        var output = TestTraceHelper.CaptureTrace(() =>
        {
            var translated = FactionsStatusScreenTranslationPatch.TranslateFactionText(
                "アンテロープ",
                "FactionsLineTranslationPatch > field=barText");

            Assert.That(translated, Is.EqualTo("アンテロープ"));
        });

        Assert.That(output, Does.Not.Contain("missing key 'アンテロープ'"));
    }

    [Test]
    public void TranslateFactionText_DoesNotLogAlreadyLocalizedFactionOutputs()
    {
        var output = TestTraceHelper.CaptureTrace(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    FactionsStatusScreenTranslationPatch.TranslateFactionText("Ailiwanの村人たち", nameof(FactionsStatusScreenTranslationPatch)),
                    Is.EqualTo("Ailiwanの村人たち"));
                Assert.That(
                    FactionsStatusScreenTranslationPatch.TranslateFactionText("評判: -500", nameof(FactionsStatusScreenTranslationPatch)),
                    Is.EqualTo("評判: -500"));
                Assert.That(
                    FactionsStatusScreenTranslationPatch.TranslateFactionText("Family of the Gauntletwaker", nameof(FactionsStatusScreenTranslationPatch)),
                    Is.EqualTo("Family of the Gauntletwaker"));
            });
        });

        Assert.Multiple(() =>
        {
            Assert.That(output, Does.Not.Contain("missing key 'Ailiwanの村人たち'"));
            Assert.That(output, Does.Not.Contain("missing key '評判: -500'"));
            Assert.That(output, Does.Not.Contain("missing key 'Family of the Gauntletwaker'"));
        });
    }

    [Test]
    public void TranslateFactionText_DoesNotDuplicateFactionContextWhenAlreadyInsideFactionRoute()
    {
        var output = TestTraceHelper.CaptureTrace(() =>
        {
            using var scope = Translator.PushLogContext(nameof(FactionsStatusScreenTranslationPatch));
            _ = FactionsStatusScreenTranslationPatch.TranslateFactionText(
                "Untranslated faction fragment",
                nameof(FactionsStatusScreenTranslationPatch));
        });

        Assert.That(output, Does.Not.Contain("FactionsStatusScreenTranslationPatch > FactionsStatusScreenTranslationPatch"));
    }

    [Test]
    public void TranslateFactionText_TranslatesTopicFragmentsInsideSecretSentences()
    {
        WriteDictionary(
            ("The {0} are interested in trading secrets about {1}.", "{0}は{1}に関する秘密の取引に関心がある。"),
            ("The {0} are interested in trading secrets about {1}. They're also interested in hearing gossip that's about them.", "{0}は{1}に関する秘密の取引に関心があり、自分たちに関するうわさ話にも興味を示す。"),
            ("The {0} are interested in learning about {1}.", "{0}は{1}について知ることに関心がある。"),
            ("The {0} are interested in sharing secrets about {1}.", "{0}は{1}に関する秘密の共有に関心がある。"),
            ("the sultan they worship", "彼らが崇拝するスルタン"),
            ("locations in the jungle", "ジャングル内の場所"),
            ("sultan they admire or despise", "彼らが好悪を抱くスルタン"),
            ("gossip that's about them", "彼ら自身に関するうわさ話"),
            ("historic sites", "史跡"),
            ("insect lair", "昆虫の巣"),
            ("ape lair", "類人猿の巣"),
            ("becoming nooks", "変成の隠れ家"),
            ("flower fields", "花畑"),
            ("ancestral bracelet Kindrish", "祖伝の腕輪キンドリシュ"),
            ("desert canyons", "砂漠峡谷"));

        var secretTrade = FactionsStatusScreenTranslationPatch.TranslateFactionText(
            "山羊人 are interested in trading secrets about locations in the jungle. They're also interested in hearing gossip that's about them.",
            nameof(FactionsStatusScreenTranslationPatch));
        var worship = FactionsStatusScreenTranslationPatch.TranslateFactionText(
            "The Alabal Bane Folk are interested in trading secrets about the sultan they worship. They're also interested in hearing gossip that's about them.",
            nameof(FactionsStatusScreenTranslationPatch));
        var topicList = FactionsStatusScreenTranslationPatch.TranslateFactionText(
            "類人猿 are interested in learning about the locations of insect lair、the locations of ape lair、sultan they admire or despise、とgossip that's about them.",
            nameof(FactionsStatusScreenTranslationPatch));
        var sharing = FactionsStatusScreenTranslationPatch.TranslateFactionText(
            "The プトゥス聖堂騎士団 are interested in sharing secrets about the locations of becoming nooksとthe locations of historic sites.",
            nameof(FactionsStatusScreenTranslationPatch));
        var learning = FactionsStatusScreenTranslationPatch.TranslateFactionText(
            "The ベイ・ラーのヒンドレン are interested in learning about locations in the flower fields、the location of the ancestral bracelet Kindrish、とgossip that's about them.",
            nameof(FactionsStatusScreenTranslationPatch));
        var trading = FactionsStatusScreenTranslationPatch.TranslateFactionText(
            "馬類 are interested in trading secrets about locations in the flower fieldsとlocations in the desert canyons.",
            nameof(FactionsStatusScreenTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(secretTrade, Is.EqualTo("山羊人はジャングル内の場所に関する秘密の取引に関心があり、自分たちに関するうわさ話にも興味を示す。"));
            Assert.That(worship, Is.EqualTo("Alabal Bane Folkは彼らが崇拝するスルタンに関する秘密の取引に関心があり、自分たちに関するうわさ話にも興味を示す。"));
            Assert.That(topicList, Is.EqualTo("類人猿は昆虫の巣の場所、類人猿の巣の場所、彼らが好悪を抱くスルタン、と彼ら自身に関するうわさ話について知ることに関心がある。"));
            Assert.That(sharing, Is.EqualTo("プトゥス聖堂騎士団は変成の隠れ家の場所と史跡の場所に関する秘密の共有に関心がある。"));
            Assert.That(learning, Is.EqualTo("ベイ・ラーのヒンドレンは花畑の場所、祖伝の腕輪キンドリシュの場所、と彼ら自身に関するうわさ話について知ることに関心がある。"));
            Assert.That(trading, Is.EqualTo("馬類は花畑の場所と砂漠峡谷の場所に関する秘密の取引に関心がある。"));
        });
    }

    [Test]
    public void TranslateFactionText_TranslatesEnglishTopicListsInsideSecretSentences()
    {
        WriteDictionary(
            ("The {0} are interested in learning about {1}.", "{0}は{1}について知ることに関心がある。"),
            ("locations in the flower fields", "花畑の場所"),
            ("the locations of insect lair", "昆虫の巣の場所"),
            ("the locations of ape lair", "類人猿の巣の場所"),
            ("sultan they admire or despise", "彼らが好悪を抱くスルタン"),
            ("gossip that's about them", "彼ら自身に関するうわさ話"));

        var translated = FactionsStatusScreenTranslationPatch.TranslateFactionText(
            "The apes are interested in learning about the locations of insect lair, the locations of ape lair, sultan they admire or despise, and gossip that's about them.",
            nameof(FactionsStatusScreenTranslationPatch));

        Assert.That(
            translated,
            Is.EqualTo("apesは昆虫の巣の場所、類人猿の巣の場所、彼らが好悪を抱くスルタン、と彼ら自身に関するうわさ話について知ることに関心がある。"));
    }

    [Test]
    public void TranslateFactionText_UsesLowerAsciiFallbackForTopicLeaves()
    {
        WriteDictionary(
            ("The {0} are interested in learning about {1}.", "{0}は{1}について知ることに関心がある。"),
            ("locations in {0}", "{0}の場所"),
            ("flower fields", "花畑"));

        var translated = FactionsStatusScreenTranslationPatch.TranslateFactionText(
            "The goatfolk are interested in learning about locations in Flower Fields.",
            nameof(FactionsStatusScreenTranslationPatch));

        Assert.That(translated, Is.EqualTo("goatfolkは花畑の場所について知ることに関心がある。"));
    }

    [Test]
    public void TranslateFactionText_TranslatesPetAndHolyPlaceSentenceSequence()
    {
        WriteDictionary(
            ("The {0} will usually let you pet them.", "{0}はたいてい撫でさせてくれる。"),
            ("The {0} won't usually let you pet them.", "{0}はたいてい撫でさせてくれない。"),
            ("You aren't welcome in their holy places.", "あなたは彼らの聖地では歓迎されていない。"),
            ("You are welcome in their holy places.", "あなたは彼らの聖地で歓迎されている。"));

        var hostile = FactionsStatusScreenTranslationPatch.TranslateFactionText(
            "猫 won't usually let you pet them. You aren't welcome in their holy places.",
            nameof(FactionsStatusScreenTranslationPatch));
        var friendly = FactionsStatusScreenTranslationPatch.TranslateFactionText(
            "犬 will usually let you pet them. You are welcome in their holy places.",
            nameof(FactionsStatusScreenTranslationPatch));

        Assert.Multiple(() =>
        {
            Assert.That(hostile, Is.EqualTo("猫はたいてい撫でさせてくれない。 あなたは彼らの聖地では歓迎されていない。"));
            Assert.That(friendly, Is.EqualTo("犬はたいてい撫でさせてくれる。 あなたは彼らの聖地で歓迎されている。"));
        });
    }

    [Test]
    public void FactionsLineDataPostfix_DoesNotLogMissingKeys_WhenRunAgainOnLocalizedLabel()
    {
        WriteDictionary(("The villagers of {0}", "{0}の村人たち"));

        var data = new DummyFactionsLineData("ignored");
        _ = data.set("Abal", "The villagers of Abal", icon: null, expanded: true);
        FactionsLineDataTranslationPatch.TranslateLineData(data);

        var output = TestTraceHelper.CaptureTrace(() => FactionsLineDataTranslationPatch.TranslateLineData(data));

        Assert.Multiple(() =>
        {
            Assert.That(data.label, Is.EqualTo("Abalの村人たち"));
            Assert.That(output, Does.Not.Contain("missing key 'Abalの村人たち'"));
        });
    }

    [Test]
    public void FactionsLineDataPostfix_UsesFactionIdFallbackForGeneratedLabels()
    {
        WriteDictionary(("SultanCult1", "スルタン教団1"));

        var data = new DummyFactionsLineData("ignored");
        _ = data.set("SultanCult1", "Cult of Baram", icon: null, expanded: true);

        FactionsLineDataTranslationPatch.TranslateLineData(data);

        Assert.Multiple(() =>
        {
            Assert.That(data.label, Is.EqualTo("スルタン教団1"));
            Assert.That(data.searchText, Does.Contain("the villagers of sultancult1"));
            Assert.That(data.searchText, Does.Contain("スルタン教団1"));
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

        var path = Path.Combine(tempDirectory, "factions-status-l2.ja.json");
        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
