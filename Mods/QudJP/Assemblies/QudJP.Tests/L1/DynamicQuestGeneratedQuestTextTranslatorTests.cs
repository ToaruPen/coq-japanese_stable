using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
[NonParallelizable]
public sealed class DynamicQuestGeneratedQuestTextTranslatorTests
{
    [SetUp]
    public void SetUp()
    {
        Translator.ResetForTests();
        ScopedDictionaryLookup.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(GetRepositoryDictionaryDirectory());
    }

    [TearDown]
    public void TearDown()
    {
        ScopedDictionaryLookup.ResetForTests();
        Translator.ResetForTests();
    }

    [TestCase("Aiding {{&Y|ドリンクス}} to Find the ポリセフian 祖父角の角笛", "{{&Y|ドリンクス}}がポリセフian 祖父角の角笛を探すのを助ける")]
    [TestCase("Helping {{&Y|ドリンクス}}", "{{&Y|ドリンクス}}を助ける")]
    [TestCase("The sanctity of salt", "塩の聖性")]
    [TestCase("The wisdom of the wheel", "車輪の叡智")]
    [TestCase("Find the rusted relic", "錆びた遺物を探す")]
    [TestCase("Return the rusted relic to Joppa", "錆びた遺物をジョッパへ返す")]
    [TestCase("Return to Joppa", "ジョッパへ戻る")]
    [TestCase("Pray At {{W|the salt shrine}}", "{{W|塩の祠}}で祈る")]
    [TestCase("Desecrate the salt shrine", "塩の祠を冒涜する")]
    [TestCase("Locate the rusted relic at {{|the rust wells}}.", "{{|錆の井戸}}で錆びた遺物を見つける。")]
    [TestCase("Locate {{|the hidden archive}}, located within 6 parasangs of {{|the Six Day Stilt}}.", "{{|隠された文書庫}}を見つける。{{|六日のスティルト}}から6パラサング以内にある。")]
    [TestCase("Locate {{|the hidden archive}}, located next to {{|the Six Day Stilt}}.", "{{|隠された文書庫}}を見つける。{{|六日のスティルト}}の隣にある。")]
    [TestCase("Locate {{|the hidden archive}}, located 4-6 parasangs north of {{|the Six Day Stilt}}.", "{{|隠された文書庫}}を見つける。{{|六日のスティルト}}から4-6パラサング北にある。")]
    [TestCase("Locate {{|the hidden archive}}, located west along the salt road that runs through {{|the Six Day Stilt}}.", "{{|隠された文書庫}}を見つける。{{|六日のスティルト}}を通る塩の道に沿って西にある。")]
    [TestCase("Return the rusted relic to Joppa and speak with Mehmet.", "錆びた遺物をジョッパへ返し、Mehmetと話す。")]
    [TestCase("Return to Joppa and speak to Mehmet.", "ジョッパへ戻り、Mehmetと話す。")]
    [TestCase("The sanctity of the Mechanimists", "メカニマス教団の聖性")]
    [TestCase("Travel to {{|the rust wells}} and pray at {{|the salt shrine}}.", "{{|錆の井戸}}へ行き、{{|塩の祠}}で祈る。")]
    [TestCase("Travel to {{|the rust wells}} and put something in {{|the chest}}.", "{{|錆の井戸}}へ行き、{{|chest}}に何かを入れる。")]
    [TestCase("Visit カルクヘタラ, Stargazerhome", "カルクヘタラ, 星見の家を訪問")]
    [TestCase("Travel to the historical site of カルクヘタラ, Stargazerhome.", "カルクヘタラ, 星見の家の史跡へ向かう。")]
    [TestCase("Travel to the historical site of カルクヘタラ, Stargazerhome", "カルクヘタラ, 星見の家の史跡へ向かう。")]
    [TestCase("Locate ダビッパ, Old Home of Window Makers", "ダビッパ, 窓職人たちの古き家を見つける")]
    [TestCase("Locate ドゥシュル, Bygone Hearth of Jewelers", "ドゥシュル, 宝石職人たちの往時の炉辺を見つける")]
    [TestCase("Recover Charmed, the Gift of 多肉植物", "多肉植物の幸運に恵まれた賜物を取り戻す")]
    [TestCase("Recover Charmed, the Gift of 多肉植物 at Luckymarsh.", "幸運な沼沢で多肉植物の幸運に恵まれた賜物を取り戻す。")]
    [TestCase("Recover Rubycus at the Briny Dunes.", "塩辛い砂丘でRubycusを取り戻す。")]
    [TestCase("Recover Inquisitive ワームwoe", "探究心の強いワームの嘆きを取り戻す")]
    [TestCase("Locate the rusted relic at {{|the spindle}}.", "{{|スピンドル}}で錆びた遺物を見つける。")]
    public void TryTranslate_TranslatesGeneratedQuestText(string source, string expected)
    {
        var translated = DynamicQuestGeneratedQuestTextTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslate_PreservesWholeSourceColorWrapper()
    {
        var translated = DynamicQuestGeneratedQuestTextTranslator.TryTranslate("{{G|Find the rusted relic}}", out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("{{G|錆びた遺物を探す}}"));
        });
    }

    [Test]
    public void TryTranslate_PrefersZoneDisplayCaptureBeforeSpaceSeparatedComponents()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "qudjp-dynamic-quest-generated-l1",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            WriteDictionaryFile(
                tempDirectory,
                "Scoped/historyspice-common.ja.json",
                ("old", "古き"),
                ("home", "家"));
            WriteDictionaryFile(tempDirectory, "ui-zone-display.ja.json", ("Old Home", "古き住処"));
            ScopedDictionaryLookup.ResetForTests();
            Translator.ResetForTests();
            Translator.SetDictionaryDirectoryForTests(tempDirectory);

            var translated = DynamicQuestGeneratedQuestTextTranslator.TryTranslate("Visit Old Home", out var result);

            Assert.Multiple(() =>
            {
                Assert.That(translated, Is.True);
                Assert.That(result, Is.EqualTo("古き住処を訪問"));
            });
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TryTranslate_StripsDirectMarkerWithoutRetranslating()
    {
        var translated = DynamicQuestGeneratedQuestTextTranslator.TryTranslate(
            MessageFrameTranslator.DirectTranslationMarker + "Find the rusted relic",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo("Find the rusted relic"));
        });
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("Seek the thing that cannot be named.")]
    public void TryTranslate_LeavesUnknownOrEmptyText(string? source)
    {
        var translated = DynamicQuestGeneratedQuestTextTranslator.TryTranslate(source, out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(source ?? string.Empty));
        });
    }

    private static string GetRepositoryDictionaryDirectory() =>
        Path.Combine(TestProjectPaths.GetRepositoryRoot(), "Mods", "QudJP", "Localization", "Dictionaries");

    private static void WriteDictionaryFile(string directory, string fileName, params (string key, string text)[] entries)
    {
        var path = Path.Combine(directory, fileName);
        var parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        var builder = new System.Text.StringBuilder();
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

        builder.Append("]}\n");
        File.WriteAllText(path, builder.ToString(), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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
