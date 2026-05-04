using System.Text;
using QudJP.Patches;

namespace QudJP.Tests.L1;

[TestFixture]
[Category("L1")]
public sealed class MessageLogProducerTranslationHelpersTests
{
    private string tempDirectory = null!;
    private string dictionaryDirectory = null!;
    private string patternFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), "qudjp-message-log-producer-l1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        dictionaryDirectory = Path.Combine(tempDirectory, "dict");
        Directory.CreateDirectory(dictionaryDirectory);
        patternFilePath = Path.Combine(tempDirectory, "messages.ja.json");

        Translator.ResetForTests();
        Translator.SetDictionaryDirectoryForTests(dictionaryDirectory);
        MessagePatternTranslator.ResetForTests();
        MessagePatternTranslator.SetPatternFileForTests(patternFilePath);
        File.WriteAllText(patternFilePath, "{\"patterns\":[]}\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    [TearDown]
    public void TearDown()
    {
        Translator.ResetForTests();
        MessagePatternTranslator.ResetForTests();

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public void TryTranslateZoneDisplayName_TranslatesCompositeComponents()
    {
        WriteExactDictionary(
            ("Joppa", "ジョッパ"),
            ("surface", "地表"));

        var translated = MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
            "Joppa, surface",
            "ZoneDisplayNameTranslationPatch",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("ジョッパ, 地表"));
        });
    }

    [Test]
    public void TryTranslateZoneDisplayName_TranslatesStrataTemplate()
    {
        WriteExactDictionary(
            ("a rusted archway", "錆びたアーチ道"),
            ("{0} strata deep", "地下{0}層"));

        var translated = MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
            "a rusted archway, 2 strata deep",
            "ZoneDisplayNameTranslationPatch",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("錆びたアーチ道, 地下2層"));
        });
    }

    [Test]
    public void TryTranslateZoneDisplayName_TranslatesStrataHighTemplate()
    {
        WriteExactDictionary(
            ("Joppa", "ジョッパ"),
            ("{0} strata high", "地上{0}層"));

        var translated = MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
            "Joppa, 3 strata high",
            "ZoneDisplayNameTranslationPatch",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("ジョッパ, 地上3層"));
        });
    }

    [TestCase("the lair of Joppa", "ジョッパの巣")]
    [TestCase("the workshop of Joppa", "ジョッパの工房")]
    [TestCase("the scriptorium of Joppa", "ジョッパの写字室")]
    [TestCase("the kitchen of Joppa", "ジョッパの厨房")]
    [TestCase("the distillery of Joppa", "ジョッパの蒸留所")]
    [TestCase("the organ market of Joppa", "ジョッパの臓器市場")]
    public void TryTranslateZoneDisplayName_TranslatesLairTemplates(string source, string expected)
    {
        WriteExactDictionary(("Joppa", "ジョッパ"));

        var translated = MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
            source,
            "ZoneDisplayNameTranslationPatch",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo(expected));
        });
    }

    [Test]
    public void TryTranslateZoneDisplayName_TranslatesFarmSuffixSegments()
    {
        WriteExactDictionary(
            ("Joppa", "ジョッパ"),
            ("farmstead", "農園"));

        var translated = MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
            "Joppa Farmstead",
            "ZoneDisplayNameTranslationPatch",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("ジョッパ農園"));
        });
    }

    [Test]
    public void TryTranslateZoneDisplayName_TranslatesRuinsTopologyForms()
    {
        WriteExactDictionary(
            ("Joppa", "ジョッパ"),
            ("red", "赤"),
            ("cross", "十字"));

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
                    "Red Joppa",
                    "ZoneDisplayNameTranslationPatch",
                    out var prefixed),
                Is.True);
            Assert.That(prefixed, Is.EqualTo("赤ジョッパ"));

            Assert.That(
                MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
                    "Joppa Cross",
                    "ZoneDisplayNameTranslationPatch",
                    out var suffixed),
                Is.True);
            Assert.That(suffixed, Is.EqualTo("ジョッパ十字"));

            Assert.That(
                MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
                    "Red Cross Joppa",
                    "ZoneDisplayNameTranslationPatch",
                    out var doubled),
                Is.True);
            Assert.That(doubled, Is.EqualTo("赤十字ジョッパ"));
        });
    }

    [Test]
    public void TryTranslateZoneDisplayName_TranslatesBiomeAdjectivesAndTails()
    {
        WriteExactDictionary(
            ("Joppa", "ジョッパ"),
            ("slimy", "ぬめる"),
            ("slime bog", "ぬめり沼"),
            ("psychically refractive", "精神屈折の"),
            ("future site", "未来址"));

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
                    "slimy Joppa",
                    "ZoneDisplayNameTranslationPatch",
                    out var fixedAdjective),
                Is.True);
            Assert.That(fixedAdjective, Is.EqualTo("ぬめるジョッパ"));

            Assert.That(
                MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
                    "Joppa and slime bog",
                    "ZoneDisplayNameTranslationPatch",
                    out var fixedTail),
                Is.True);
            Assert.That(fixedTail, Is.EqualTo("ジョッパとぬめり沼"));

            Assert.That(
                MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
                    "psychically refractive Joppa",
                    "ZoneDisplayNameTranslationPatch",
                    out var psychicAdjective),
                Is.True);
            Assert.That(psychicAdjective, Is.EqualTo("精神屈折のジョッパ"));

            Assert.That(
                MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
                    "Joppa and future site",
                    "ZoneDisplayNameTranslationPatch",
                    out var psychicTail),
                Is.True);
            Assert.That(psychicTail, Is.EqualTo("ジョッパと未来址"));
        });
    }

    [Test]
    public void TryTranslateZoneDisplayName_TranslatesGoatfolkSuffixWithJapaneseComma()
    {
        WriteExactDictionary(
            ("Joppa", "ジョッパ"),
            (", goatfolk village", "、ヤギ人の村"));

        var translated = MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
            "Joppa, goatfolk village",
            "ZoneDisplayNameTranslationPatch",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("ジョッパ、ヤギ人の村"));
        });
    }

    [Test]
    public void TryTranslateZoneDisplayName_TranslatesStaticZonePhrasesAndSegments()
    {
        WriteExactDictionary(
            ("some forgotten ruins", "忘れられた遺跡"),
            ("abandoned village", "廃村"),
            ("outskirts", "外れ"),
            ("sky", "上空"),
            ("undervillage", "地下集落"),
            ("liminal floor", "境界層"));

        Assert.Multiple(() =>
        {
            Assert.That(
                MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
                    "some forgotten ruins",
                    "ZoneDisplayNameTranslationPatch",
                    out var ruins),
                Is.True);
            Assert.That(ruins, Is.EqualTo("忘れられた遺跡"));

            Assert.That(
                MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
                    "abandoned village",
                    "ZoneDisplayNameTranslationPatch",
                    out var village),
                Is.True);
            Assert.That(village, Is.EqualTo("廃村"));

            Assert.That(
                MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
                    "outskirts, sky, undervillage, liminal floor",
                    "ZoneDisplayNameTranslationPatch",
                    out var layered),
                Is.True);
            Assert.That(layered, Is.EqualTo("外れ, 上空, 地下集落, 境界層"));
        });
    }

    [Test]
    public void TryTranslateZoneDisplayName_ReturnsFalseForEmptyInput()
    {
        var translated = MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
            string.Empty,
            "ZoneDisplayNameTranslationPatch",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.False);
            Assert.That(result, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public void TryTranslateZoneDisplayName_PreservesColorTags()
    {
        WriteExactDictionary(
            ("Joppa", "ジョッパ"),
            ("surface", "地表"));

        var translated = MessageLogProducerTranslationHelpers.TryTranslateZoneDisplayName(
            "<color=#ff0>Joppa</color>, <color=#0f0>surface</color>",
            "ZoneDisplayNameTranslationPatch",
            out var result);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(result, Is.EqualTo("<color=#ff0>ジョッパ</color>, <color=#0f0>地表</color>"));
        });
    }

    [Test]
    public void PreparePassByMessage_MarksTranslatedMessage()
    {
        WritePatternDictionary(("^You pass by (.+?)[.!]?$", "{0}のそばを通り過ぎた。"));

        var result = MessageLogProducerTranslationHelpers.PreparePassByMessage(
            "You pass by ウォーターヴァイン.",
            "PhysicsEnterCellPassByTranslationPatch");

        Assert.That(result, Is.EqualTo("\u0001ウォーターヴァインのそばを通り過ぎた。"));
    }

    [Test]
    public void PreparePassByMessage_PreservesColorTags()
    {
        WritePatternDictionary(("^You pass by (.+?)[.!]?$", "{0}のそばを通り過ぎた。"));

        var result = MessageLogProducerTranslationHelpers.PreparePassByMessage(
            "You pass by <color=#ff0>ウォーターヴァイン</color>.",
            "PhysicsEnterCellPassByTranslationPatch");

        Assert.That(result, Is.EqualTo("\u0001<color=#ff0>ウォーターヴァイン</color>のそばを通り過ぎた。"));
    }

    [Test]
    public void PreparePassByMessage_PreservesDirectTranslationMarker()
    {
        const string source = "\u0001You pass by ウォーターヴァイン.";

        var result = MessageLogProducerTranslationHelpers.PreparePassByMessage(
            source,
            "PhysicsEnterCellPassByTranslationPatch");

        Assert.That(result, Is.EqualTo(source));
    }

    [Test]
    public void TryPreparePatternMessage_StripsLeadingControlHeaderBeforePatternTranslation()
    {
        WritePatternDictionary(("^(?:The |the |[Aa]n? )?(.+?) (?:has|have) nothing to trade[.!]?$", "{0}には取引するものがない"));

        var source = "\u0002have\u001F10\u001F14\u001F\u0003The 濡れた 光葉 has nothing to trade.";

        var translated = MessageLogProducerTranslationHelpers.TryPreparePatternMessage(
            ref source,
            "PopupShowTranslationPatch",
            "Popup");

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(source, Is.EqualTo("\u0001濡れた 光葉には取引するものがない"));
        });
    }

    [Test]
    public void TryPreparePatternMessage_StripsLeadingControlHeader_ForColorizedPlayerHitWithRoll()
    {
        UseRepositoryPatternDictionary();

        var source = "\u0002hit\u001F7\u001F18\u001F\u0003{{g|You hit {{&w|(x1)}} for 1 damage with your {{w|青銅の短剣}}! [18]}}";

        var translated = MessageLogProducerTranslationHelpers.TryPreparePatternMessage(
            ref source,
            nameof(GameObjectEmitMessageTranslationPatch),
            "EmitMessage",
            markJapaneseAsDirect: true);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(
                source,
                Is.EqualTo("\u0001{{g|{{w|青銅の短剣}}で1ダメージを与えた。({{&w|x1}}) [18]}}"));
        });
    }

    [Test]
    public void TryPreparePatternMessage_DoesNotReportNoPattern_ForAlreadyLocalizedJapaneseMessage()
    {
        const string localized =
            "狂信者が叫んだ「信仰心があるなら聖遺物を大聖堂の司祭に届けよ！貴様の穢れを清めるのだ！」";
        var source = localized;

        var translated = MessageLogProducerTranslationHelpers.TryPreparePatternMessage(
            ref source,
            nameof(GameObjectEmitMessageTranslationPatch),
            "EmitMessage",
            markJapaneseAsDirect: true);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(source, Is.EqualTo(MessageFrameTranslator.MarkDirectTranslation(localized)));
            Assert.That(MessagePatternTranslator.GetMissingPatternHitCountForTests(localized), Is.EqualTo(0));
        });
    }

    [Test]
    public void TryPreparePatternMessage_DoesNotReportNoPattern_ForAlreadyLocalizedJapaneseMessageWithTrailingLbsToken()
    {
        const string localized = "装備重量: 10 lbs.";
        var source = localized;

        var translated = MessageLogProducerTranslationHelpers.TryPreparePatternMessage(
            ref source,
            nameof(GameObjectEmitMessageTranslationPatch),
            "EmitMessage",
            markJapaneseAsDirect: true);

        Assert.Multiple(() =>
        {
            Assert.That(translated, Is.True);
            Assert.That(source, Is.EqualTo(MessageFrameTranslator.MarkDirectTranslation(localized)));
            Assert.That(MessagePatternTranslator.GetMissingPatternHitCountForTests(localized), Is.EqualTo(0));
        });
    }

    [Test]
    public void PrepareZoneBannerMessage_MarksAlreadyLocalizedBanner()
    {
        var result = MessageLogProducerTranslationHelpers.PrepareZoneBannerMessage(
            "ジョッパ, 地表, 06:00",
            "ZoneManagerSetActiveZoneTranslationPatch");

        Assert.That(result, Is.EqualTo("\u0001ジョッパ, 地表, 06:00"));
    }

    [Test]
    public void PrepareZoneBannerMessage_ReturnsEmptyInputUnchanged()
    {
        var result = MessageLogProducerTranslationHelpers.PrepareZoneBannerMessage(
            string.Empty,
            "ZoneManagerSetActiveZoneTranslationPatch");

        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void PrepareZoneBannerMessage_PreservesColorTags()
    {
        WriteExactDictionary(
            ("Joppa", "ジョッパ"),
            ("surface", "地表"));

        var result = MessageLogProducerTranslationHelpers.PrepareZoneBannerMessage(
            "<color=#ff0>Joppa</color>, <color=#0f0>surface</color>, 06:00",
            "ZoneManagerSetActiveZoneTranslationPatch");

        Assert.That(result, Is.EqualTo("\u0001<color=#ff0>ジョッパ</color>, <color=#0f0>地表</color>, 06:00"));
    }

    [Test]
    public void PrepareZoneBannerMessage_PreservesDirectTranslationMarker()
    {
        const string source = "\u0001ジョッパ, 地表, 06:00";

        var result = MessageLogProducerTranslationHelpers.PrepareZoneBannerMessage(
            source,
            "ZoneManagerSetActiveZoneTranslationPatch");

        Assert.That(result, Is.EqualTo(source));
    }

    private void WriteExactDictionary(params (string key, string text)[] entries)
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

        File.WriteAllText(
            Path.Combine(dictionaryDirectory, "ui-message-log-producer-l1.ja.json"),
            builder.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void WritePatternDictionary(params (string pattern, string template)[] entries)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        builder.Append("\"patterns\":[");

        for (var index = 0; index < entries.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"pattern\":\"");
            builder.Append(EscapeJson(entries[index].pattern));
            builder.Append("\",\"template\":\"");
            builder.Append(EscapeJson(entries[index].template));
            builder.Append("\"}");
        }

        builder.Append("]}");
        builder.AppendLine();

        File.WriteAllText(patternFilePath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void UseRepositoryPatternDictionary()
    {
        var root = TestProjectPaths.GetRepositoryRoot();
        var repositoryPatternFile = Path.Combine(root, "Mods", "QudJP", "Localization", "Dictionaries", "messages.ja.json");
        File.Copy(repositoryPatternFile, patternFilePath, overwrite: true);
    }

    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
